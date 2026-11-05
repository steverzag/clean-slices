using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence.SaveChanges;

internal interface ISaveChangesStep
{
	public int Order { get; }

	public ValueTask BeforeSaveAsync(DbContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

	public ValueTask AfterCommitAsync(DbContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

	public ValueTask SaveFailedAsync(DbContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
