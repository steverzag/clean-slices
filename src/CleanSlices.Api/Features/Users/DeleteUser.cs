using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Api.Extensions;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Users;

public class DeleteUser : IEndpoint
{
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var user = await context.Users.FindAsync([id], cancellationToken);
		if (user is null)
		{
			return UserErrors.NotFound(id).ToProblem();
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
