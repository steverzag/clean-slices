#if (message_broker && !outbox)
using CleanSlices.Infrastructure.Persistence.AfterCommit;

namespace CleanSlices.Infrastructure.IntegrationEvents.Broker;

internal sealed class AfterCommitPublisher(IAfterCommit afterCommit, IMessageDispatcher dispatcher) : IIntegrationEventPublisher
{
	public Task Publish(IIntegrationEvent integrationEvent, CancellationToken ct = default)
	{
		var message = new IntegrationEventMessage(Guid.CreateVersion7(), integrationEvent);
		if (afterCommit.IsSaving)
		{
			afterCommit.Enqueue(token => dispatcher.Dispatch(message, token));
			return Task.CompletedTask;
		}

		return dispatcher.Dispatch(message, ct);
	}
}
#endif
