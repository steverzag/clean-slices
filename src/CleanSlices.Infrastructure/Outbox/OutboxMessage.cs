#if (outbox)
namespace CleanSlices.Infrastructure.Outbox;

internal sealed class OutboxMessage
{
	public Guid Id { get; init; } = Guid.CreateVersion7();
	public required string Name { get; init; }
	public required string Payload { get; init; }
	public string? TraceParent { get; init; }
	public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

	public int Attempts { get; set; }
	public string? LastError { get; set; }
	public DateTimeOffset? NextAttemptAt { get; set; }
	public DateTimeOffset? LockedUntil { get; set; }
	public string? LockedBy { get; set; }
	public DateTimeOffset? ProcessedAt { get; set; }
	public DateTimeOffset? FailedAt { get; set; }
}
#endif
