#if (integration_events)
namespace CleanSlices.Infrastructure.IntegrationEvents;

public interface IIntegrationEventHandler<in TEvent>
	where TEvent : IIntegrationEvent
{
	public Task Handle(TEvent integrationEvent, CancellationToken ct);
}
#endif
