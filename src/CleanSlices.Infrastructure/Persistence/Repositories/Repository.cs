#if (repository)
using CleanSlices.Core.Domain;
using CleanSlices.Core.Shared;
using CleanSlices.Infrastructure.Pagination;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence.Repositories;

// The methods every repository gets; entity-specific queries go in the entity's own repository.
internal abstract class Repository<T>(AppDbContext context) : IRepository<T> where T : Entity
{
	protected AppDbContext Context => context;

	protected DbSet<T> Set => context.Set<T>();

	public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
		await Set.FindAsync([id], cancellationToken);

	public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
		Set.AnyAsync(e => e.Id == id, cancellationToken);

	public async Task<Page<T>> GetPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
	{
		var page = await Set
			.AsNoTracking()
			.OrderByDescending(e => e.CreatedAt)
			.ThenByDescending(e => e.Id)
			.PaginateAsync(pageNumber, pageSize, cancellationToken);

		return new Page<T>([.. page], page.PageNumber, page.PageSize, page.TotalCount);
	}

	public void Add(T entity) => Set.Add(entity);

	public void Remove(T entity) => Set.Remove(entity);
}
#endif
