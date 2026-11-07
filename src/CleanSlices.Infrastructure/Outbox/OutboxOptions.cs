#if (outbox)
namespace CleanSlices.Infrastructure.Outbox;

internal sealed class OutboxOptions
{
	public const string Section = "Outbox";

	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
	public int BatchSize { get; set; } = 20;
	public int MaxAttempts { get; set; } = 5;
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
	public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);
	public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);
}
#endif
