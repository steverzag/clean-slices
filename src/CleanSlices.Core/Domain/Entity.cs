using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain;

public abstract class Entity
{
	public Guid Id { get; init; } = Guid.CreateVersion7();
	public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
	private readonly List<IDomainEvent> _domainEvents = [];
	public IReadOnlyList<IDomainEvent> DomainEvents => [.. _domainEvents];

	public void ClearDomainEvents()
	{
		_domainEvents.Clear();
	}

	protected void Raise(IDomainEvent domainEvent)
	{
		_domainEvents.Add(domainEvent);
	}
}
