using CleanSlices.Core.Domain;
using CleanSlices.Core.Messages;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CleanSlices.Infrastructure.Persistence.Configuration;

internal sealed class DomainEventDispatchInterceptor(IPublisher publisher) : SaveChangesInterceptor
{
	private List<Entity> _raisedBy = [];

	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
		DbContextEventData eventData, 
		InterceptionResult<int> result, 
		CancellationToken cancellationToken = default)
	{
		_raisedBy = eventData.Context is null
			? []
			: [.. eventData.Context.ChangeTracker
				.Entries<Entity>()
				.Select(e => e.Entity)
				.Where(e => e.DomainEvents.Count > 0)];

		return base.SavingChangesAsync(eventData, result, cancellationToken);
	}

	public override async ValueTask<int> SavedChangesAsync(
		SaveChangesCompletedEventData eventData,
		int result,
		CancellationToken cancellationToken = default)
	{
		var raisedBy = _raisedBy;
		_raisedBy = [];

		var domainEvents = raisedBy.SelectMany(e => e.DomainEvents).ToList();

		foreach (var entity in raisedBy)
		{
			entity.ClearDomainEvents();
		}

		foreach(var domainEvent in domainEvents)
		{
			await publisher.Publish(domainEvent, CancellationToken.None);
		}

		return await base.SavedChangesAsync(eventData, result, cancellationToken);
	}

	public override Task SaveChangesFailedAsync(
		DbContextErrorEventData eventData, 
		CancellationToken cancellationToken = default)
	{
		_raisedBy = [];
		return base.SaveChangesFailedAsync(eventData, cancellationToken);
	}

	public override InterceptionResult<int> SavingChanges(
		DbContextEventData eventData,
		InterceptionResult<int> result) =>
		throw new NotSupportedException(
			$"{nameof(AppDbContext)} dispatches domain events after the commit, which cannot " +
			$"run synchronously. Use {nameof(AppDbContext.SaveChangesAsync)}.");

}
