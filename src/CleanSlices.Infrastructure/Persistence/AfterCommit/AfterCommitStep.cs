#if (after_commit)
using CleanSlices.Infrastructure.Persistence.SaveChanges;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CleanSlices.Infrastructure.Persistence.AfterCommit;

internal sealed class AfterCommitStep(AfterCommitActions actions, ILogger<AfterCommitStep> logger) : ISaveChangesStep
{
	public int Order => 1000;

	// The data is already saved: a failing action is logged, and the others still run.
	public async ValueTask AfterCommitAsync(DbContext context, CancellationToken cancellationToken)
	{
		foreach (var action in actions.TakeAll())
		{
			try
			{
				await action(cancellationToken);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				logger.LogError(ex, "An action queued to run after the commit failed; the data was saved");
			}
		}
	}

	public ValueTask SaveFailedAsync(DbContext context, CancellationToken cancellationToken)
	{
		actions.TakeAll();
		return ValueTask.CompletedTask;
	}
}
#endif
