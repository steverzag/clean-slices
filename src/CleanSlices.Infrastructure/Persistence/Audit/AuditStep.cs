#if (audit)
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CleanSlices.Core.Domain;
using CleanSlices.Infrastructure.CurrentUser;
using CleanSlices.Infrastructure.Persistence.SaveChanges;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CleanSlices.Infrastructure.Persistence.Audit;

// After domain events and soft delete, so it records what is really saved: a soft delete is an update.
internal sealed class AuditStep(ICurrentUser currentUser) : ISaveChangesStep
{
	private static readonly JsonSerializerOptions SnapshotOptions = new() { Converters = { new JsonStringEnumConverter() } };

	public int Order => 300;

	public ValueTask BeforeSaveAsync(DbContext context, CancellationToken cancellationToken)
	{
		// Left over from a save that failed on this context: saving again would record the change twice.
		foreach (var stale in context.ChangeTracker.Entries<AuditEntry>().Where(e => e.State == EntityState.Added).ToList())
		{
			stale.State = EntityState.Detached;
		}

		var timestamp = DateTimeOffset.UtcNow;
		var traceId = Activity.Current?.TraceId.ToString();

		var entries = context.ChangeTracker.Entries<IAuditable>()
			.Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
			.Select(e => new AuditEntry(
				e.Metadata.ClrType.Name,
				EntityId(e),
				Action(e.State),
				e.State == EntityState.Added ? null : Snapshot(e, original: true),
				e.State == EntityState.Deleted ? null : Snapshot(e, original: false),
				currentUser.UserId,
				timestamp,
				traceId))
			.ToList();

		context.AddRange(entries);
		return ValueTask.CompletedTask;
	}

	private static AuditAction Action(EntityState state) => state switch
	{
		EntityState.Added => AuditAction.Added,
		EntityState.Deleted => AuditAction.Deleted,
		_ => AuditAction.Modified,
	};

	private static string EntityId(EntityEntry entry) =>
		string.Join(",", entry.Metadata.FindPrimaryKey()!.Properties.Select(p => entry.Property(p.Name).CurrentValue));

	// Mapped columns only, from EF's change tracking: never the domain events or computed properties.
	private static string Snapshot(EntityEntry entry, bool original)
	{
		var values = entry.Properties
			.Where(p => p.Metadata.PropertyInfo?.GetCustomAttribute<NotAuditedAttribute>() is null)
			.ToDictionary(p => p.Metadata.Name, p => original ? p.OriginalValue : p.CurrentValue);

		return JsonSerializer.Serialize(values, SnapshotOptions);
	}
}
#endif
