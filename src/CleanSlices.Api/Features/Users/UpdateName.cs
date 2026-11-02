using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Users;

public class UpdateName : IEndpoint
{
	public record UpdateUserNameRequest(string Name);

	public async Task<IResult> Handler(AppDbContext context, Guid id, UpdateUserNameRequest request, CancellationToken cancellationToken)
	{
		var user = await context.Users.FindAsync([id], cancellationToken);
		if (user is null)
		{
			throw UserErrors.NotFound(id);
		}

		user.UpdateName(request.Name);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/users/{id:guid}/name", Handler)
			.WithTags("Users")
			.WithName(nameof(UpdateName));
	}
}
