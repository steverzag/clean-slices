#if (signalr)
//#if (samples)
using CleanSlices.Infrastructure.EventHandlers.TaskItems;
//#endif
using Microsoft.AspNetCore.SignalR;

namespace CleanSlices.Api.Configuration.Realtime;

// Clients connect here to receive messages; the server sends them through IRealtimeNotifier.
public sealed class NotificationsHub : Hub
{
	//#if (samples)
	// Any client may watch any task: fine for a sample. Check access before joining a group in real code.
	public Task WatchTask(Guid taskId) =>
		Groups.AddToGroupAsync(Context.ConnectionId, TaskWatchers.Group(taskId));
	//#endif
}
#endif
