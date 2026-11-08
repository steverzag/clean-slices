#if (after_commit)
namespace CleanSlices.Infrastructure.Persistence.AfterCommit;

internal sealed class AfterCommitActions : IAfterCommit
{
	private readonly List<Func<CancellationToken, Task>> _queued = [];

	public bool IsSaving { get; set; }

	public void Enqueue(Func<CancellationToken, Task> action)
	{
		ArgumentNullException.ThrowIfNull(action);
		_queued.Add(action);
	}

	public IReadOnlyList<Func<CancellationToken, Task>> TakeAll()
	{
		var taken = _queued.ToList();
		_queued.Clear();
		return taken;
	}
}
#endif
