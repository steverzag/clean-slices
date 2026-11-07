#if (repository)
namespace CleanSlices.Core.Domain;

// Repositories never save: the handler ends the work with one call.
public interface IUnitOfWork
{
	public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
#endif
