using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
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
#if (result)
			? UserErrors.NotFound(id).ToProblem()
#else
			? throw UserErrors.NotFound(id)
#endif
			: Results.Ok(user);
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/users/{id:guid}", Handler)
			.WithTags("Users")
			.WithName(nameof(GetUserById));
	}
}
