#if (outbox)
using System.Diagnostics;
using CleanSlices.Infrastructure.IntegrationEvents;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Infrastructure.Outbox;

// Adds a row to the current save, so the event is stored only if the data is.
internal sealed class OutboxPublisher(AppDbContext context, IntegrationEventSerializer serializer) : IIntegrationEventPublisher
{
	public Task Publish(IIntegrationEvent integrationEvent, CancellationToken ct = default)
	{
		var (name, payload) = serializer.Serialize(integrationEvent);
		context.Set<OutboxMessage>().Add(new OutboxMessage
		{
			Name = name,
			Payload = payload,
			TraceParent = Activity.Current?.Id,
		});

		return Task.CompletedTask;
	}
}
#endif
