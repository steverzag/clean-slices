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
<!--#if (servicebus)-->
## Azure Service Bus

The AppHost runs the Service Bus **emulator** (`AddAzureServiceBus("messaging").RunAsEmulator()`) and declares the topic and the subscription there: the emulator can't create them at run time. The API gets its connection string as `ConnectionStrings:messaging`. The code is in `Infrastructure/IntegrationEvents/ServiceBus/`, with the official client (`Azure.Messaging.ServiceBus`, MIT) through its Aspire integration.

### Topology

| What | Name (setting) | Kind |
|---|---|---|
| Where the app publishes | `cleanslices-events` (`Messaging:Topic`) | topic |
| Where the app receives | `cleanslices-api` (`Messaging:Subscription`) | subscription of the topic, receiving every message |
| Where failed messages end up | the subscription's dead-letter queue | built into Service Bus |

The app doesn't create them: in the AppHost for the emulator, and in Azure with the namespace (Aspire's provisioning, or your own infrastructure). **Keep the names and `MaxDeliveryCount` in the AppHost in step with the `Messaging` settings.**

### Sending and receiving

- **Sending**: when `SendMessageAsync` returns, Service Bus has the message.
- **Receiving**: a background service (`ServiceBusConsumer`, a `ServiceBusProcessor`) runs the event's handlers and completes the message once they all succeed.
- **When a handler fails**, the message is abandoned and delivered again. After the subscription's `MaxDeliveryCount` (5, set in the AppHost) Service Bus moves it to the dead-letter queue.
- **A message that can't be read** (an event name the app doesn't know, a payload that isn't valid JSON) goes to the dead-letter queue at once.
- In Azure, look at the dead-letter queue with the portal's Service Bus Explorer. The emulator has no UI: read it with the SDK (`ServiceBusReceiverOptions.SubQueue = SubQueue.DeadLetter`), as the tests do.
- **License**: Service Bus is a paid Azure service. The emulator runs under Microsoft's emulator license, which the AppHost and the tests accept for you; it's for development only.

<!--#endif-->
## The message

| Part | Value |
|---|---|
| Body | The event as JSON (camelCase) |
<!--#if (servicebus)-->
| Subject | The event's name: the class name, or `[IntegrationEventName("...")]` |
<!--#else-->
| Type | The event's name: the class name, or `[IntegrationEventName("...")]`; also the routing key |
<!--#endif-->
<!--#if (outbox)-->
| Message ID | The outbox row's ID, the same on every redelivery |
<!--#else-->
| Message ID | A new ID per event, the same on every redelivery |
<!--#endif-->
| `traceparent` (header, or application property) | The trace of the request that published it, so the dashboard shows the handling in the same trace |

<!--#if (servicebus)-->
A service that only wants some events adds its own subscription to this app's topic, with a filter on `Subject`.
<!--#else-->
A service that only wants some events binds its own queue to this app's exchange with their names.
<!--#endif-->

<!--#if (outbox)-->
## With the outbox

The outbox sends each stored event to the broker and marks it processed once the broker confirms. If the broker can't be reached, the outbox retries later (see [outbox.md](outbox.md)): no event is lost, even when the broker is down while the data is saved.
<!--#else-->
## Without the outbox

An event published during a save (from a domain event handler) is sent once that save commits (`AfterCommitPublisher`, through `IAfterCommit`), and never for a save that failed. An event published outside a save (in an endpoint or a background job, with no data to wait for) is sent at once, and a failure reaches the caller. **If the broker can't be reached when a committed save's event is sent, or the app stops right after the commit, the event is lost** (the failure is logged). Add the outbox when every event must arrive.
<!--#endif-->

## Settings

All optional, in the `Messaging` section of `appsettings.json`; the defaults are in `IntegrationEvents/Broker/BrokerOptions.cs`.

| Setting | Default |
|---|---|
| `Topic` | `cleanslices-events` |
| `Subscription` | `cleanslices-api` |
<!--#if (servicebus)-->
| `MaxDeliveries` | 5; not used by Service Bus, where it's the subscription's `MaxDeliveryCount`, set in the AppHost |
<!--#else-->
| `MaxDeliveries` | 5 |
<!--#endif-->
| `Prefetch` | 10 messages fetched ahead; they're handled one at a time |

## Trade-offs

<!--#if (inbox)-->
- **At least once**: a message can be delivered more than once (a failing handler, a consumer that stops before acknowledging). The inbox makes each handler run once per message.
<!--#else-->
- **At least once**: a message can be delivered more than once (a failing handler, a consumer that stops before acknowledging). Handlers must tolerate it, or add the inbox.
<!--#endif-->
- Integration events are public contracts once other services receive them: add fields, never rename or remove them.
- Redeliveries are immediate; a handler that fails because something is briefly down uses up its deliveries quickly. Retry inside the handler for that, or let the dead-letter queue collect them.
