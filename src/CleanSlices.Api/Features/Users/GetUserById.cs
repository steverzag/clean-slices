using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Api.Extensions;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Users;

public class GetUserById : IEndpoint
{
	public record GetUserByIdResponse(Guid Id, string Name, string Email, DateTimeOffset CreatedAt);

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

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/users/{id:guid}", Handler)
			.WithTags("Users")
			.WithName(nameof(GetUserById));
	}
}
