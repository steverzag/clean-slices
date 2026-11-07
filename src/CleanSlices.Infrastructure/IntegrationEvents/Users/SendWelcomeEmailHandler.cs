#if (integration_events)
using Microsoft.Extensions.Logging;

namespace CleanSlices.Infrastructure.IntegrationEvents.Users;

// Stands in for an email provider. A real one is called last, and may run twice for the same event.
internal sealed class SendWelcomeEmailHandler(ILogger<SendWelcomeEmailHandler> logger) : IIntegrationEventHandler<UserCreatedIntegrationEvent>
{
	public Task Handle(UserCreatedIntegrationEvent integrationEvent, CancellationToken ct)
	{
		logger.LogInformation("Welcome email for {Name} <{Email}>", integrationEvent.Name, integrationEvent.Email);
		return Task.CompletedTask;
	}
}
#endif
