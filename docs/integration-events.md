# Integration events

Integration events are for the outside world: emails, other services, anything that should happen **after** the data is saved and can be retried. Domain events keep the app itself consistent.

| | Domain events | Integration events |
|---|---|---|
| Purpose | Keep the app consistent | Side effects and other services |
| When | Before the commit, in the same transaction | After the commit |
| If a handler fails | The whole save fails | Retried; the data is already saved |
| Handlers may | Change tracked entities, never call `SaveChanges` | Do anything, including external calls |

Everything lives in `src/CleanSlices.Infrastructure/IntegrationEvents/`.

<!--#if (message_broker && outbox)-->
**Delivery:** the outbox stores each event with the data and sends it to the broker; the broker delivers it to the handlers. See [outbox.md](outbox.md) and [messaging.md](messaging.md).
<!--#elseif (message_broker)-->
**Delivery:** each event is sent to the broker once the save that published it commits (at once when it was published outside a save), and the broker delivers it to the handlers. See [messaging.md](messaging.md).
<!--#else-->
**Delivery:** the outbox stores each event with the data, and its processor runs the handlers in the background. See [outbox.md](outbox.md).
<!--#endif-->

## Publishing an event

Publish from a domain event handler. The event is sent only if the save that raised the domain event succeeds.

```csharp
public sealed record OrderPlacedIntegrationEvent(Guid OrderId, string CustomerEmail, decimal Total) : IIntegrationEvent;

internal sealed class PublishOrderPlacedHandler(AppDbContext context, IIntegrationEventPublisher publisher)
	: INotificationHandler<OrderPlacedDomainEvent>
{
	public async Task Handle(OrderPlacedDomainEvent notification, CancellationToken cancellationToken)
	{
		if (await context.Set<Order>().FindAsync([notification.Id], cancellationToken) is { } order)
		{
			await publisher.Publish(new OrderPlacedIntegrationEvent(order.Id, order.CustomerEmail, order.Total), cancellationToken);
		}
	}
}
```

- **Put in the event what its handlers need.** They run later, when the entity may have changed or been deleted.
- **Events are public contracts.** Add properties; never rename or remove them. Rows already stored, and other services, still use the old shape. A property an old event lacks gets its default value.

## Handling an event

```csharp
internal sealed class SendOrderConfirmationHandler(IEmailSender email) : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
	public Task Handle(OrderPlacedIntegrationEvent integrationEvent, CancellationToken ct) =>
		email.SendAsync(integrationEvent.CustomerEmail, "Thanks for your order", ct);
}
```

Handlers are found by scanning; an event can have several. Each one runs in its own scope, with its own `AppDbContext`.

Delivery is **at least once**: a handler can run again for an event it already handled (after a crash, or when another handler of the same event failed). So:

- **Save at most once, at the end.** Two saves can't be committed together: if the process stops between them, the first is kept and the second lost.
- **Make external calls last**, after the save, and make them safe to repeat (an email provider's idempotency key, an upsert in the other service).

## Names

An event is stored under its class name. To rename the class without breaking events already stored, or to give other services a deliberate name, set it:

```csharp
[IntegrationEventName("orders.placed.v1")]
public sealed record OrderPlacedIntegrationEvent(...) : IIntegrationEvent;
```

Two events with the same name stop the app at startup. A stored event whose name no class has any more (it was deleted or renamed without the attribute) is marked failed instead of retried.
