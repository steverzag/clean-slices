#if (after_commit)
namespace CleanSlices.Infrastructure.Persistence.AfterCommit;

// Work that must only happen once the data is saved: removing a cached copy, sending a message.
// Queued during a save (from a domain event handler), run after the commit, dropped if the save fails.
public interface IAfterCommit
{
	// True while a save runs: work for the outside world waits for its commit; otherwise it can run at once.
	public bool IsSaving { get; }

	public void Enqueue(Func<CancellationToken, Task> action);
}
#endif
