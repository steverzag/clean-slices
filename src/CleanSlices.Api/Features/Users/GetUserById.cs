using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
#if (repository)
using CleanSlices.Core.Domain.Users;
#endif
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Users;

public class GetUserById : IEndpoint
{
	public record GetUserByIdResponse(Guid Id, string Name, string Email, DateTimeOffset CreatedAt);

#if (repository && result)
	public async Task<IResult> Handler(IUserRepository users, Guid id, CancellationToken cancellationToken)
	{
		var user = await users.GetByIdAsync(id, cancellationToken);

		return user is null
			? UserErrors.NotFound(id).ToProblem()
			: Results.Ok(new GetUserByIdResponse(user.Id, user.Name, user.Email, user.CreatedAt));
	}
#elif (repository)
	public async Task<IResult> Handler(IUserRepository users, Guid id, CancellationToken cancellationToken)
	{
		var user = await users.GetByIdAsync(id, cancellationToken);

		return user is null
			? throw UserErrors.NotFound(id)
			: Results.Ok(new GetUserByIdResponse(user.Id, user.Name, user.Email, user.CreatedAt));
	}
#elif (result)
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var user = await context.Users
			.AsNoTracking()
			.Where(u => u.Id == id)
			.Select(u => new GetUserByIdResponse(u.Id, u.Name, u.Email, u.CreatedAt))
			.FirstOrDefaultAsync(cancellationToken);

		return user is null
			? UserErrors.NotFound(id).ToProblem()
			: Results.Ok(user);
	}
#else
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var user = await context.Users
			.AsNoTracking()
			.Where(u => u.Id == id)
			.Select(u => new GetUserByIdResponse(u.Id, u.Name, u.Email, u.CreatedAt))
			.FirstOrDefaultAsync(cancellationToken);

		return user is null
			? throw UserErrors.NotFound(id)
			: Results.Ok(user);
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/users/{id:guid}", Handler)
			.WithTags("Users")
			.WithName(nameof(GetUserById));
	}
}
