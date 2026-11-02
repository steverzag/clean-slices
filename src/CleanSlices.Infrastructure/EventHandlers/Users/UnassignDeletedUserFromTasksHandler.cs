using CleanSlices.Core.Domain.Users;
using CleanSlices.Core.Messages;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.EventHandlers.Users;

internal sealed class UnassignDeletedUserFromTasksHandler(AppDbContext context)
	: INotificationHandler<UserDeletedDomainEvent>
{
	public async Task Handle(UserDeletedDomainEvent notification, CancellationToken cancellationToken)
	{
		var tasks = await context.Tasks
			.Include(t => t.UserAssignments)
			.Where(t => t.UserAssignments.Any(a => a.UserId == notification.Id))
			.ToListAsync(cancellationToken);

		if (tasks.Count == 0)
		{
			return;
		}

		foreach (var task in tasks)
		{
			task.UnassignUser(notification.Id);
		}

		await context.SaveChangesAsync(cancellationToken);
	}
}
