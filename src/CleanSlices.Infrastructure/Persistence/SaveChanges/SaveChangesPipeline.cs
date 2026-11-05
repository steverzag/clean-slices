using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CleanSlices.Infrastructure.Persistence.SaveChanges;

internal sealed class SaveChangesPipeline(IEnumerable<ISaveChangesStep> steps) : SaveChangesInterceptor
{
	private readonly IReadOnlyList<ISaveChangesStep> _steps = [.. steps.OrderBy(step => step.Order)];
	private bool _running;

	public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
		DbContextEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		var context = eventData.Context;
		if (context is null)
		{
			return result;
		}

		if (_running)
		{
			throw new InvalidOperationException(
				"SaveChanges was called while the save pipeline was running, which is not allowed. " +
				"Domain event handlers or any other flow should not call SaveChanges until the pipeline has completed.");
		}

		_running = true;
		try
		{
			foreach (var step in _steps)
			{
				await step.BeforeSaveAsync(context, cancellationToken);
			}

			// Steps and handlers may have changed properties of tracked entities.
			context.ChangeTracker.DetectChanges();
		}
		finally
		{
			_running = false;
		}

		return result;
	}

	public override async ValueTask<int> SavedChangesAsync(
		SaveChangesCompletedEventData eventData,
		int result,
		CancellationToken cancellationToken = default)
	{
		if (eventData.Context is { } context)
		{
			foreach (var step in _steps)
			{
				await step.AfterCommitAsync(context, cancellationToken);
			}
		}

		return result;
	}

	public override async Task SaveChangesFailedAsync(
		DbContextErrorEventData eventData,
		CancellationToken cancellationToken = default)
	{
		if (eventData.Context is { } context)
		{
			foreach (var step in _steps)
			{
				await step.SaveFailedAsync(context, cancellationToken);
			}
		}
	}

	public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
		throw new NotSupportedException(
			$"The save pipeline runs asynchronous steps, which can't run inside a synchronous save. " +
			$"Use {nameof(DbContext.SaveChangesAsync)}.");
}
