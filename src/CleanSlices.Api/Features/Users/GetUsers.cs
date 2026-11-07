#if (!repository)
using CleanSlices.Api.Configuration;
#endif
using CleanSlices.Api.Configuration.Endpoints;
#if (repository)
using CleanSlices.Core.Domain.Users;
#endif
using CleanSlices.Infrastructure.Pagination;
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Users;

public class GetUsers : IEndpoint
{
	public record UserListItemResponse(Guid Id, string Name, string Email, DateTimeOffset CreatedAt);

#if (repository)
	public async Task<IResult> Handler(IUserRepository users, [AsParameters] PaginationParameters paging, CancellationToken cancellationToken)
	{
		var page = await users.GetPageAsync(paging.PageNumber, paging.PageSize, cancellationToken);

		return Results.Ok(page.Map(u => new UserListItemResponse(u.Id, u.Name, u.Email, u.CreatedAt)));
	}
#else
	public async Task<IResult> Handler(AppDbContext context, [AsParameters] PaginationParameters paging, CancellationToken cancellationToken)
	{
		var users = await context.Users
			.AsNoTracking()
			.OrderByDescending(u => u.CreatedAt)
			.ThenByDescending(u => u.Id)
			.Select(u => new UserListItemResponse(u.Id, u.Name, u.Email, u.CreatedAt))
			.PaginateAsync(paging, cancellationToken);

		return Results.Ok(users.ToResponse());
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/users", Handler)
			.WithTags("Users")
			.WithName(nameof(GetUsers));
	}
}
