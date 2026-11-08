#if (signalr)
namespace CleanSlices.Infrastructure.Realtime;

// Pushes a message to connected clients. During a save (from a domain event handler) it waits for the commit,
// and is dropped if the save fails; outside a save it is sent at once. Clients that aren't connected miss it.
public interface IRealtimeNotifier
{
	public Task SendToUserAsync(string userId, string method, object payload, CancellationToken cancellationToken = default);

	public Task SendToGroupAsync(string group, string method, object payload, CancellationToken cancellationToken = default);

	public Task SendToAllAsync(string method, object payload, CancellationToken cancellationToken = default);
}
#endif
