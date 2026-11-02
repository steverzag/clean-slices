using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Messages;
using Microsoft.Extensions.Logging;

namespace CleanSlices.Infrastructure.EventHandlers.TaskItems;

internal sealed class TaskItemCompletedEventHandler(ILogger<TaskItemCompletedEventHandler> logger) : INotificationHandler<TaskItemCompletedDomainEvent>
{
	public Task Handle(TaskItemCompletedDomainEvent notification, CancellationToken cancellationToken)
	{
		logger.LogInformation("Task item {TaskItemId} completed", notification.Id);
		return Task.CompletedTask;
	}
}
