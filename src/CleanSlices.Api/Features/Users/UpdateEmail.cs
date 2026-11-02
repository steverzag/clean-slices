using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Users;

public class UpdateEmail : IEndpoint
{
	public record UpdateUserEmailRequest(string Email);

	public async Task<IResult> Handler(AppDbContext context, Guid id, UpdateUserEmailRequest request, CancellationToken cancellationToken)
	{
		var user = await context.Users.FindAsync([id], cancellationToken);
		if (user is null)
		{
			throw UserErrors.NotFound(id);
		}

		if (await context.Users.AnyAsync(u => u.Email == request.Email && u.Id != id, cancellationToken))
		{
			throw UserErrors.EmailInUse(request.Email);
		}

		user.UpdateEmail(request.Email);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/users/{id:guid}/email", Handler)
			.WithTags("Users")
			.WithName(nameof(UpdateEmail));
	}
}
