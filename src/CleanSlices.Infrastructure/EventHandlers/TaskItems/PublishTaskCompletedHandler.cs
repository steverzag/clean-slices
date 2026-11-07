#if (integration_events)
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Messages;
using CleanSlices.Infrastructure.IntegrationEvents;
using CleanSlices.Infrastructure.IntegrationEvents.TaskItems;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.EventHandlers.TaskItems;

internal sealed class PublishTaskCompletedHandler(AppDbContext context, IIntegrationEventPublisher publisher)
	: INotificationHandler<TaskItemCompletedDomainEvent>
{
	public async Task Handle(TaskItemCompletedDomainEvent notification, CancellationToken cancellationToken)
	{
		if (await context.Tasks.FindAsync([notification.Id], cancellationToken) is not { } task)
		{
			return;
		}

		// The assignees when the task was completed: a snapshot, whatever happens to the task later.
		var assigneeIds = await context.Set<TaskItemUserAssignment>()
			.Where(a => a.TaskItemId == task.Id)
			.Select(a => a.UserId)
			.ToListAsync(cancellationToken);

		await publisher.Publish(new TaskCompletedIntegrationEvent(task.Id, task.Title, assigneeIds), cancellationToken);
	}
}
#endif
