#if (repository)
namespace CleanSlices.Core.Domain.Tasks;

public interface ITaskItemRepository : IRepository<TaskItem>
{
	public Task<TaskItem?> GetWithAssignmentsAsync(Guid id, CancellationToken cancellationToken = default);
}
#endif
