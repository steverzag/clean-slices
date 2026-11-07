#if (integration_events)
namespace CleanSlices.Infrastructure.IntegrationEvents;

public interface IIntegrationEventPublisher
{
	public Task Publish(IIntegrationEvent integrationEvent, CancellationToken ct = default);
}
#endif
