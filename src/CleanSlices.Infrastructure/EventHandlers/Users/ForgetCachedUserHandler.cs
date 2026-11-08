#if (hybrid_cache)
using CleanSlices.Core.Domain.Users;
using CleanSlices.Core.Messages;
using CleanSlices.Infrastructure.Persistence.AfterCommit;
using Microsoft.Extensions.Caching.Hybrid;

namespace CleanSlices.Infrastructure.EventHandlers.Users;

public static class UserCache
{
	public static string Key(Guid id) => $"user:{id}";
}

// Removed after the commit: removed earlier, a read before the commit could cache the old values again.
internal sealed class ForgetCachedUserHandler(HybridCache cache, IAfterCommit afterCommit)
	: INotificationHandler<UserUpdatedDomainEvent>, INotificationHandler<UserDeletedDomainEvent>
{
	public Task Handle(UserUpdatedDomainEvent notification, CancellationToken cancellationToken) => Forget(notification.Id);

	public Task Handle(UserDeletedDomainEvent notification, CancellationToken cancellationToken) => Forget(notification.Id);

	private Task Forget(Guid id)
	{
		afterCommit.Enqueue(ct => cache.RemoveAsync(UserCache.Key(id), ct).AsTask());
		return Task.CompletedTask;
	}
}
#endif
