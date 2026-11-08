#if (signalr)
using CleanSlices.Infrastructure.Persistence.AfterCommit;
using CleanSlices.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace CleanSlices.Api.Configuration.Realtime;

internal sealed class SignalRNotifier(IHubContext<NotificationsHub> hub, IAfterCommit afterCommit) : IRealtimeNotifier
{
	public Task SendToUserAsync(string userId, string method, object payload, CancellationToken cancellationToken = default) =>
		Send(clients => clients.User(userId), method, payload, cancellationToken);

	public Task SendToGroupAsync(string group, string method, object payload, CancellationToken cancellationToken = default) =>
		Send(clients => clients.Group(group), method, payload, cancellationToken);

	public Task SendToAllAsync(string method, object payload, CancellationToken cancellationToken = default) =>
		Send(clients => clients.All, method, payload, cancellationToken);

	// Never announce a change that might not be saved.
	private Task Send(Func<IHubClients, IClientProxy> target, string method, object payload, CancellationToken cancellationToken)
	{
		if (afterCommit.IsSaving)
		{
			afterCommit.Enqueue(token => target(hub.Clients).SendAsync(method, payload, token));
			return Task.CompletedTask;
		}

		return target(hub.Clients).SendAsync(method, payload, cancellationToken);
	}
}
#endif
