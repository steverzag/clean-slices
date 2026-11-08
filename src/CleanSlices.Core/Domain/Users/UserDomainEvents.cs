using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Users;

public sealed record UserCreatedDomainEvent(Guid Id) : IDomainEvent;
public sealed record UserUpdatedDomainEvent(Guid Id) : IDomainEvent;
public sealed record UserDeletedDomainEvent(Guid Id) : IDomainEvent;
