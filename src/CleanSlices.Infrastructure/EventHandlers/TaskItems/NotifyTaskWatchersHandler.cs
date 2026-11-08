#if (signalr)
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Messages;
using CleanSlices.Infrastructure.Realtime;

namespace CleanSlices.Infrastructure.EventHandlers.TaskItems;

public static class TaskWatchers
{
	public const string TaskChanged = nameof(TaskChanged);

	public static string Group(Guid taskId) => $"task:{taskId}";
}

public sealed record TaskChangedMessage(Guid TaskId, string Change);

// Clients watching a task (see the hub's WatchTask) hear about it once the change is saved.
internal sealed class NotifyTaskWatchersHandler(IRealtimeNotifier notifier)
	: INotificationHandler<TaskItemCompletedDomainEvent>, INotificationHandler<TaskItemDeletedDomainEvent>
{
	public Task Handle(TaskItemCompletedDomainEvent notification, CancellationToken cancellationToken) =>
		Notify(notification.Id, "Completed", cancellationToken);

	public Task Handle(TaskItemDeletedDomainEvent notification, CancellationToken cancellationToken) =>
		Notify(notification.Id, "Deleted", cancellationToken);

	private Task Notify(Guid taskId, string change, CancellationToken cancellationToken) =>
		notifier.SendToGroupAsync(TaskWatchers.Group(taskId), TaskWatchers.TaskChanged, new TaskChangedMessage(taskId, change), cancellationToken);
}
#endif
