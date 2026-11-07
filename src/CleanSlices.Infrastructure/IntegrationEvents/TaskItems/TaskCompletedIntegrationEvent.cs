#if (integration_events)
namespace CleanSlices.Infrastructure.IntegrationEvents.TaskItems;

public sealed record TaskCompletedIntegrationEvent(Guid TaskId, string Title, IReadOnlyList<Guid> AssigneeIds) : IIntegrationEvent;
#endif
