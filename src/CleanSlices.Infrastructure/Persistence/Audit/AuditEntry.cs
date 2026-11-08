#if (audit)
namespace CleanSlices.Infrastructure.Persistence.Audit;

public enum AuditAction
{
	Added,
	Modified,
	Deleted,
}

// One change to an auditable entity. Written by the save pipeline, in the same transaction as the change.
public sealed class AuditEntry
{
	public const int EntityTypeMaxLength = 200;
	public const int EntityIdMaxLength = 200;
	public const int UserIdMaxLength = 200;
	public const int TraceIdMaxLength = 64;

	public Guid Id { get; private set; } = Guid.CreateVersion7();
	public string EntityType { get; private set; } = null!;
	public string EntityId { get; private set; } = null!;
	public AuditAction Action { get; private set; }
	public string? Before { get; private set; }
	public string? After { get; private set; }
	public string? UserId { get; private set; }
	public DateTimeOffset Timestamp { get; private set; }
	public string? TraceId { get; private set; }

	private AuditEntry() { } // For EF Core

	internal AuditEntry(string entityType, string entityId, AuditAction action, string? before, string? after, string? userId, DateTimeOffset timestamp, string? traceId)
	{
		EntityType = entityType;
		EntityId = entityId;
		Action = action;
		Before = before;
		After = after;
		UserId = userId;
		Timestamp = timestamp;
		TraceId = traceId;
	}
}
#endif
