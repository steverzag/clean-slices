#if (integration_events)
namespace CleanSlices.Infrastructure.IntegrationEvents;

internal sealed record IntegrationEventMessage(Guid Id, IIntegrationEvent Event);

// How a message reaches its handlers: in process by default, through a broker with a queue option.
internal interface IMessageDispatcher
{
	public Task Dispatch(IntegrationEventMessage message, CancellationToken ct);
}

internal sealed class InProcessMessageDispatcher(IntegrationEventHandlers handlers) : IMessageDispatcher
{
	public Task Dispatch(IntegrationEventMessage message, CancellationToken ct) => handlers.RunAsync(message, ct);
}
#endif
