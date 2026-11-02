using CleanSlices.Core.Messages;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace CleanSlices.Infrastructure.Messages;

internal sealed class Publisher(IServiceProvider provider) : IPublisher
{
	private static readonly ConcurrentDictionary<Type, NotificationDispatcher> _dispatchers = [];
	public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification
	{
		ArgumentNullException.ThrowIfNull(notification);

		var dispatcher = _dispatchers.GetOrAdd(
				notification.GetType(),
				static notificationType => (NotificationDispatcher)Activator.CreateInstance(
					typeof(NotificationDispatcher<>).MakeGenericType(notificationType))!
			);

		return dispatcher.Dispatch(notification, provider, ct);
	}

	private abstract class NotificationDispatcher
	{
		public abstract Task Dispatch(INotification notification, IServiceProvider sp, CancellationToken ct);
	}

	private sealed class NotificationDispatcher<TNotification> : NotificationDispatcher 
		where TNotification : INotification
	{
		public override async Task Dispatch(INotification notification, IServiceProvider sp, CancellationToken ct)
		{
			var handlers = sp.GetServices<INotificationHandler<TNotification>>();
			foreach (var handler in handlers)
			{
				await handler.Handle((TNotification)notification, ct);
			}
		}
	}
}
