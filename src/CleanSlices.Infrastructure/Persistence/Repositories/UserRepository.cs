#if (repository)
using CleanSlices.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AppDbContext context) : Repository<User>(context), IUserRepository
{
	public Task<bool> IsEmailInUseAsync(string email, CancellationToken cancellationToken = default) =>
		Set.AnyAsync(u => u.Email == email, cancellationToken);

	public Task<bool> IsEmailInUseAsync(string email, Guid exceptUserId, CancellationToken cancellationToken = default) =>
		Set.AnyAsync(u => u.Email == email && u.Id != exceptUserId, cancellationToken);
}
#endif
