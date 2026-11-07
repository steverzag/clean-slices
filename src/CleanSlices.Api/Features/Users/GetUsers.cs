#if (!repository || cqrs)
using CleanSlices.Api.Configuration;
#endif
using CleanSlices.Api.Configuration.Endpoints;
#if (cqrs)
using CleanSlices.Core.Domain;
#endif
#if (repository || cqrs)
using CleanSlices.Core.Domain.Users;
#endif
using CleanSlices.Infrastructure.Pagination;
#if (!repository && !cqrs)
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Users;

public class GetUsers : IEndpoint
{
	public record UserListItemResponse(Guid Id, string Name, string Email, DateTimeOffset CreatedAt);

#if (cqrs)
	public sealed record GetUsersQuery(int PageNumber, int PageSize) : IQuery<PaginationResponse<UserListItemResponse>>;

	internal sealed class GetUsersHandler(IReadDb db) : IQueryHandler<GetUsersQuery, PaginationResponse<UserListItemResponse>>
	{
		public async ValueTask<PaginationResponse<UserListItemResponse>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
		{
			var users = await db.Query<User>()
				.OrderByDescending(u => u.CreatedAt)
				.ThenByDescending(u => u.Id)
				.Select(u => new UserListItemResponse(u.Id, u.Name, u.Email, u.CreatedAt))
				.PaginateAsync(query.PageNumber, query.PageSize, cancellationToken);

			return users.ToResponse();
		}
	}

	public async Task<IResult> Handler(ISender sender, [AsParameters] PaginationParameters paging, CancellationToken cancellationToken) =>
		Results.Ok(await sender.Send(new GetUsersQuery(paging.PageNumber, paging.PageSize), cancellationToken));
#elif (repository)
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
