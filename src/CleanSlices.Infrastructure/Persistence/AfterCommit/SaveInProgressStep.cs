#if (after_commit)
using CleanSlices.Infrastructure.Persistence.SaveChanges;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence.AfterCommit;

// First of all steps, so domain event handlers already see the save as running.
internal sealed class SaveInProgressStep(AfterCommitActions actions) : ISaveChangesStep
{
	public int Order => int.MinValue;

	public ValueTask BeforeSaveAsync(DbContext context, CancellationToken cancellationToken)
	{
		actions.IsSaving = true;
		return ValueTask.CompletedTask;
	}

	public ValueTask AfterCommitAsync(DbContext context, CancellationToken cancellationToken)
	{
		actions.IsSaving = false;
		return ValueTask.CompletedTask;
	}

	public ValueTask SaveFailedAsync(DbContext context, CancellationToken cancellationToken)
	{
		actions.IsSaving = false;
		return ValueTask.CompletedTask;
	}
}
#endif
