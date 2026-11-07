#if (integration_events)
namespace CleanSlices.Infrastructure.IntegrationEvents.Users;

// Only the id: other systems forget the user without receiving their personal data again.
// A deliberate name for other services, independent of the class name.
[IntegrationEventName("users.deleted.v1")]
public sealed record UserDeletedIntegrationEvent(Guid UserId) : IIntegrationEvent;
#endif
