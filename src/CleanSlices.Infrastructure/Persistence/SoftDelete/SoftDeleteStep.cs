#if (soft_delete)
using CleanSlices.Core.Domain;
using CleanSlices.Infrastructure.Persistence.SaveChanges;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence.SoftDelete;

// After domain events (their handlers may delete too), before audit (which then records an update).
internal sealed class SoftDeleteStep : ISaveChangesStep
{
	public int Order => 100;

	public ValueTask BeforeSaveAsync(DbContext context, CancellationToken cancellationToken)
	{
		foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>().Where(e => e.State == EntityState.Deleted))
		{
			entry.State = EntityState.Unchanged;
			entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = DateTimeOffset.UtcNow;
		}

		return ValueTask.CompletedTask;
	}
}
#endif
