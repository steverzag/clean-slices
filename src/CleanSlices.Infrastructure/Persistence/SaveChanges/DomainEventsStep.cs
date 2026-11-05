using CleanSlices.Core.Domain;
using CleanSlices.Core.Messages;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence.SaveChanges;

internal sealed class DomainEventsStep(IPublisher publisher) : ISaveChangesStep
{
	private const int MaxRounds = 10;

	public int Order => 0;

	public async ValueTask BeforeSaveAsync(DbContext context, CancellationToken cancellationToken)
	{
		for (var round = 1; ; round++)
		{
			var raisedBy = context.ChangeTracker
				.Entries<Entity>()
				.Select(entry => entry.Entity)
				.Where(entity => entity.DomainEvents.Count > 0)
				.ToList();

			if (raisedBy.Count == 0)
			{
				return;
			}

			if (round > MaxRounds)
			{
				throw new InvalidOperationException(
					$"Domain events were still being raised after {MaxRounds} rounds. Check for handlers that raise each other's events.");
			}

			var domainEvents = raisedBy.SelectMany(entity => entity.DomainEvents).ToList();
			foreach (var entity in raisedBy)
			{
				entity.ClearDomainEvents();
			}

			foreach (var domainEvent in domainEvents)
			{
				await publisher.Publish(domainEvent, cancellationToken);
			}
		}
	}
}
