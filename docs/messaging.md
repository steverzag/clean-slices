# Messaging

Integration events go through a **message broker**: the app sends each event to the broker, and receives the events it handles from its own queue. Other services can receive them too. Domain events never go through the broker; they always run in the app, before the commit.

How to publish and handle events is in [integration-events.md](integration-events.md).

<!--#if (rabbitmq)-->
## RabbitMQ

The AppHost runs RabbitMQ in a container with the management UI (`AddRabbitMQ("messaging")`); the API gets its connection string as `ConnectionStrings:messaging`. The code is in `Infrastructure/IntegrationEvents/RabbitMq/`, with the official client (`RabbitMQ.Client`, Apache 2.0 / MPL 2.0) through its Aspire integration.

### Topology

The app declares it the first time it publishes or receives:

| What | Name (setting) | Kind |
|---|---|---|
| Where the app publishes | `cleanslices-events` (`Messaging:Topic`) | durable topic exchange |
| Where the app receives | `cleanslices-api` (`Messaging:Subscription`) | quorum queue, one binding per integration event the app knows (the routing key is the event's name) |
| Where failed messages end up | `cleanslices-api.dead-letter` | quorum queue, through a fanout exchange of the same name |

### Sending and receiving

- **Sending** waits for the broker's confirmation (publisher confirms), and messages are persistent.
- **Receiving**: a background service (`RabbitMqConsumer`) runs the event's handlers and acknowledges the message once they all succeed.
- **When a handler fails**, the message goes back to the queue and is delivered again, right away. The queue counts deliveries: after `Messaging:MaxDeliveries` (5 by default) the message goes to the dead-letter queue. There is no delay between deliveries.
- **A message that can't be read** (an event name the app doesn't know, a payload that isn't valid JSON) goes to the dead-letter queue at once.
- Look at the dead-letter queue in the management UI (the RabbitMQ resource in the Aspire dashboard). To retry messages from it, move them back to the queue with a shovel (the `rabbitmq_shovel_management` plugin adds *Move messages* to the UI) or your own tool.

<!--#endif-->
## The message

| Part | Value |
|---|---|
| Body | The event as JSON (camelCase) |
| Type | The event's name: the class name, or `[IntegrationEventName("...")]` |
<!--#if (outbox)-->
| Message ID | The outbox row's ID, the same on every redelivery |
<!--#else-->
| Message ID | A new ID per event, the same on every redelivery |
<!--#endif-->
| `traceparent` header | The trace of the request that published it, so the dashboard shows the handling in the same trace |

A service that only wants some events binds its own queue to this app's exchange with their names.

<!--#if (outbox)-->
## With the outbox

The outbox sends each stored event to the broker and marks it processed once the broker confirms. If the broker can't be reached, the outbox retries later (see [outbox.md](outbox.md)): no event is lost, even when the broker is down while the data is saved.
<!--#else-->
## Without the outbox

An event is sent once the save that published it commits (`AfterCommitPublisher`, through `IAfterCommit`), and never for a save that failed. **If the broker can't be reached at that moment, or the app stops right after the commit, the event is lost** (the failure is logged). Add the outbox when every event must arrive.
<!--#endif-->

## Settings

All optional, in the `Messaging` section of `appsettings.json`; the defaults are in `IntegrationEvents/Broker/BrokerOptions.cs`.

| Setting | Default |
|---|---|
| `Topic` | `cleanslices-events` |
| `Subscription` | `cleanslices-api` |
| `MaxDeliveries` | 5 |
| `Prefetch` | 10 messages handled at a time |

## Trade-offs

<!--#if (inbox)-->
- **At least once**: a message can be delivered more than once (a failing handler, a consumer that stops before acknowledging). The inbox makes each handler run once per message.
<!--#else-->
- **At least once**: a message can be delivered more than once (a failing handler, a consumer that stops before acknowledging). Handlers must tolerate it, or add the inbox.
<!--#endif-->
- Integration events are public contracts once other services receive them: add fields, never rename or remove them.
- Redeliveries are immediate; a handler that fails because something is briefly down uses up its deliveries quickly. Retry inside the handler for that, or let the dead-letter queue collect them.
