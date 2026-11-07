#if (repository)
namespace CleanSlices.Core.Domain.Users;

public interface IUserRepository : IRepository<User>
{
	public Task<bool> IsEmailInUseAsync(string email, CancellationToken cancellationToken = default);

	public Task<bool> IsEmailInUseAsync(string email, Guid exceptUserId, CancellationToken cancellationToken = default);
}
#endif
