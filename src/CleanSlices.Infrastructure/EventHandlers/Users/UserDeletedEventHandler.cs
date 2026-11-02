using CleanSlices.Core.Domain.Users;
using CleanSlices.Core.Messages;
using Microsoft.Extensions.Logging;

namespace CleanSlices.Infrastructure.EventHandlers.Users;

internal sealed class UserDeletedEventHandler(ILogger<UserDeletedEventHandler> logger) : INotificationHandler<UserDeletedDomainEvent>
{
	public Task Handle(UserDeletedDomainEvent notification, CancellationToken cancellationToken)
	{
		logger.LogInformation("User {UserId} deleted", notification.Id);
		return Task.CompletedTask;
	}
}
