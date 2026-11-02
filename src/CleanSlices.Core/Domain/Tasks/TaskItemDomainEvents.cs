using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Tasks;

public sealed record TaskItemCreatedDomainEvent(Guid Id) : IDomainEvent;
public sealed record TaskItemDeletedDomainEvent(Guid Id) : IDomainEvent;
public sealed record TaskItemCompletedDomainEvent(Guid Id) : IDomainEvent;
