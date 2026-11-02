using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Messages;
using Microsoft.Extensions.Logging;

namespace CleanSlices.Infrastructure.EventHandlers.TaskItems;

internal sealed class TaskItemDeletedEventHandler(ILogger<TaskItemDeletedEventHandler> logger) : INotificationHandler<TaskItemDeletedDomainEvent>
{
	public Task Handle(TaskItemDeletedDomainEvent notification, CancellationToken cancellationToken)
	{
		logger.LogInformation("Task item {TaskItemId} deleted", notification.Id);
		return Task.CompletedTask;
	}
}
