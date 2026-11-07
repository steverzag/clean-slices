#if (inbox)
namespace CleanSlices.Infrastructure.Inbox;

internal sealed class InboxOptions
{
	public const string Section = "Inbox";

	public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);
}
#endif
