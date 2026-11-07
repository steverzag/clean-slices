#if (integration_events)
using CleanSlices.Core.Domain.Users;
using CleanSlices.Core.Messages;
using CleanSlices.Infrastructure.IntegrationEvents;
using CleanSlices.Infrastructure.IntegrationEvents.Users;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Infrastructure.EventHandlers.Users;

internal sealed class PublishUserCreatedHandler(AppDbContext context, IIntegrationEventPublisher publisher)
	: INotificationHandler<UserCreatedDomainEvent>
{
	public async Task Handle(UserCreatedDomainEvent notification, CancellationToken cancellationToken)
	{
		// Found without a query: the user is tracked, it's being saved right now.
		if (await context.Users.FindAsync([notification.Id], cancellationToken) is { } user)
		{
			// The event carries what its handlers need, so they never read a user that may have changed since.
			await publisher.Publish(new UserCreatedIntegrationEvent(user.Id, user.Name, user.Email), cancellationToken);
		}
	}
}
#endif
