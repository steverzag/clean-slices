#if (after_commit)
namespace CleanSlices.Infrastructure.Persistence.AfterCommit;

// Work that must only happen once the data is saved: removing a cached copy, sending a message.
// Queued during a save (from a domain event handler), run after the commit, dropped if the save fails.
public interface IAfterCommit
{
	public void Enqueue(Func<CancellationToken, Task> action);
}
#endif
