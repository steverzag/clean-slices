#if (integration_events)
using Microsoft.Extensions.Logging;

namespace CleanSlices.Infrastructure.IntegrationEvents.Users;

internal sealed class ForgetUserHandler(ILogger<ForgetUserHandler> logger) : IIntegrationEventHandler<UserDeletedIntegrationEvent>
{
	public Task Handle(UserDeletedIntegrationEvent integrationEvent, CancellationToken ct)
	{
		logger.LogInformation("Removing user {UserId} from the mailing list", integrationEvent.UserId);
		return Task.CompletedTask;
	}
}
#endif
