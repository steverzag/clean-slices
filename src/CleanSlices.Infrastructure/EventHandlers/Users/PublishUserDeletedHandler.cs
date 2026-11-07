#if (integration_events)
using CleanSlices.Core.Domain.Users;
using CleanSlices.Core.Messages;
using CleanSlices.Infrastructure.IntegrationEvents;
using CleanSlices.Infrastructure.IntegrationEvents.Users;

namespace CleanSlices.Infrastructure.EventHandlers.Users;

internal sealed class PublishUserDeletedHandler(IIntegrationEventPublisher publisher)
	: INotificationHandler<UserDeletedDomainEvent>
{
	public Task Handle(UserDeletedDomainEvent notification, CancellationToken cancellationToken) =>
		publisher.Publish(new UserDeletedIntegrationEvent(notification.Id), cancellationToken);
}
#endif
