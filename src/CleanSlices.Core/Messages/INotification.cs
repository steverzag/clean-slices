
namespace CleanSlices.Core.Messages;

public interface INotification;

public interface INotificationHandler<in TNotification>
	where TNotification : INotification
{
	public Task Handle(TNotification notification, CancellationToken ct);
}

public interface IPublisher
{
	public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
		where TNotification : INotification;
}
