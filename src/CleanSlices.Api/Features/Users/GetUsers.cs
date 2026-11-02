using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Users;

public class GetUsers : IEndpoint
{
	public record UserListItemResponse(Guid Id, string Name, string Email, DateTimeOffset CreatedAt);

	public async Task<IResult> Handler(AppDbContext context, CancellationToken cancellationToken)
	{
		var users = await context.Users
			.AsNoTracking()
			.Select(u => new UserListItemResponse(u.Id, u.Name, u.Email, u.CreatedAt))
			.ToListAsync(cancellationToken);

		return Results.Ok(users);
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/users", Handler)
			.WithTags("Users")
			.WithName(nameof(GetUsers));
	}
}
