using CleanSlices.Api.Configuration;
using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Infrastructure.Pagination;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Users;

public class GetUsers : IEndpoint
{
	public record UserListItemResponse(Guid Id, string Name, string Email, DateTimeOffset CreatedAt);

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

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/users", Handler)
			.WithTags("Users")
			.WithName(nameof(GetUsers));
	}
}
