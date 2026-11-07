#if (idempotency)
namespace CleanSlices.Api.Configuration.Idempotency;

public sealed class IdempotencyOptions
{
	public const string Section = "Idempotency";

	public bool Required { get; set; }
	public TimeSpan Retention { get; set; } = TimeSpan.FromHours(24);
}
#endif
