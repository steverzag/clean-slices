using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Shared;
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
			return UserErrors.NotFound(id).ToProblem();
		}

		var result = await user.UpdateName(request.Name)
			.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/users/{id:guid}/name", Handler)
			.WithTags("Users")
			.WithName(nameof(UpdateName));
	}
}
