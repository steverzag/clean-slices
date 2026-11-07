#if (inbox)
namespace CleanSlices.Infrastructure.Inbox;

internal sealed class ProcessedMessage
{
	public required Guid MessageId { get; init; }
	public required string Handler { get; init; }
	public DateTimeOffset ProcessedAt { get; init; } = DateTimeOffset.UtcNow;
}
#endif
