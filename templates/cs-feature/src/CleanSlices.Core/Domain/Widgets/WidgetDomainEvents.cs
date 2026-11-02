using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Widgets;

public sealed record WidgetCreatedDomainEvent(Guid Id) : IDomainEvent;
public sealed record WidgetDeletedDomainEvent(Guid Id) : IDomainEvent;
