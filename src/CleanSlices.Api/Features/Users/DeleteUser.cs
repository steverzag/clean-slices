using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Users;

public class DeleteUser : IEndpoint
{
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var user = await context.Users.FindAsync([id], cancellationToken);
		if (user is null)
		{
#if (result)
			return UserErrors.NotFound(id).ToProblem();
#else
			throw UserErrors.NotFound(id);
#endif
		}

		user.Delete();
		context.Users.Remove(user);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapDelete("/users/{id:guid}", Handler)
			.WithTags("Users")
			.WithName(nameof(DeleteUser));
	}
}
