#if (message_broker && !outbox)
using CleanSlices.Infrastructure.Persistence.AfterCommit;

namespace CleanSlices.Infrastructure.IntegrationEvents.Broker;

// Without the outbox: the event is sent once the data is committed, and never for a save that failed.
// If the broker can't be reached then, the event is lost (logged); the outbox is the reliable choice.
internal sealed class AfterCommitPublisher(IAfterCommit afterCommit, IMessageDispatcher dispatcher) : IIntegrationEventPublisher
{
	public Task Publish(IIntegrationEvent integrationEvent, CancellationToken ct = default)
	{
		var message = new IntegrationEventMessage(Guid.CreateVersion7(), integrationEvent);
		afterCommit.Enqueue(token => dispatcher.Dispatch(message, token));
		return Task.CompletedTask;
	}
}
#endif
