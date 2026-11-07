#if (repository)
using CleanSlices.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence.Repositories;

internal sealed class TaskItemRepository(AppDbContext context) : Repository<TaskItem>(context), ITaskItemRepository
{
	public Task<TaskItem?> GetWithAssignmentsAsync(Guid id, CancellationToken cancellationToken = default) =>
		Set.Include(t => t.UserAssignments).FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
}
#endif
