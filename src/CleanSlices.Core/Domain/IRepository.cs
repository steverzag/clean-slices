#if (repository)
using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain;

public interface IRepository<T> where T : Entity
{
	public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

	public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

	// Newest first, read only.
	public Task<Page<T>> GetPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

	public void Add(T entity);

	public void Remove(T entity);
}
#endif
