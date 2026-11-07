#if (integration_events)
namespace CleanSlices.Infrastructure.IntegrationEvents.Users;

public sealed record UserCreatedIntegrationEvent(Guid UserId, string Name, string Email) : IIntegrationEvent;
#endif
