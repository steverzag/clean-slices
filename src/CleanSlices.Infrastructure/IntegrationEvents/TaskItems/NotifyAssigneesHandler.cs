#if (integration_events)
using Microsoft.Extensions.Logging;

namespace CleanSlices.Infrastructure.IntegrationEvents.TaskItems;

internal sealed class NotifyAssigneesHandler(ILogger<NotifyAssigneesHandler> logger) : IIntegrationEventHandler<TaskCompletedIntegrationEvent>
{
	public Task Handle(TaskCompletedIntegrationEvent integrationEvent, CancellationToken ct)
	{
		foreach (var assigneeId in integrationEvent.AssigneeIds)
		{
			logger.LogInformation("Notifying user {UserId}: task \"{Title}\" was completed", assigneeId, integrationEvent.Title);
		}

		return Task.CompletedTask;
	}
}
#endif
