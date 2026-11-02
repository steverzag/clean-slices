using CleanSlices.Core.Domain.Users;
using CleanSlices.Core.Messages;
using Microsoft.Extensions.Logging;

namespace CleanSlices.Infrastructure.EventHandlers.Users;

internal sealed class UserCreatedEventHandler(ILogger<UserCreatedEventHandler> logger) : INotificationHandler<UserCreatedDomainEvent>
{
	public Task Handle(UserCreatedDomainEvent notification, CancellationToken cancellationToken)
	{
		logger.LogInformation("User {UserId} created", notification.Id);
		return Task.CompletedTask;
	}
}
