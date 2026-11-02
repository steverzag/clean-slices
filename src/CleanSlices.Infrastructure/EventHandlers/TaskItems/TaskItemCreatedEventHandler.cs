using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Messages;
using Microsoft.Extensions.Logging;

namespace CleanSlices.Infrastructure.EventHandlers.TaskItems;

internal sealed class TaskItemCreatedEventHandler(ILogger<TaskItemCreatedEventHandler> logger) : INotificationHandler<TaskItemCreatedDomainEvent>
{
	public Task Handle(TaskItemCreatedDomainEvent notification, CancellationToken cancellationToken)
	{
		logger.LogInformation("Task item {TaskItemId} created", notification.Id);
		return Task.CompletedTask;
	}
}
