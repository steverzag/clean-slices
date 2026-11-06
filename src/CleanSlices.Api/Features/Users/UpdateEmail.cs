using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Shared;
#endif
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Users;

public class UpdateEmail : IEndpoint
{
	public record UpdateUserEmailRequest(string Email);

#if (result)
	public async Task<IResult> Handler(AppDbContext context, Guid id, UpdateUserEmailRequest request, CancellationToken cancellationToken)
	{
		var user = await context.Users.FindAsync([id], cancellationToken);
		if (user is null)
		{
			return UserErrors.NotFound(id).ToProblem();
		}

		if (await context.Users.AnyAsync(u => u.Email == request.Email && u.Id != id, cancellationToken))
		{
			return UserErrors.EmailInUse(request.Email).ToProblem();
		}

		var result = await user.UpdateEmail(request.Email)
			.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}
#else
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
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/users/{id:guid}/email", Handler)
			.WithTags("Users")
			.WithName(nameof(UpdateEmail));
	}
}
