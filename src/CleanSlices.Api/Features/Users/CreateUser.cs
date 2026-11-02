using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Domain.Users;
using CleanSlices.Core.Shared;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Users;

public class CreateUser : IEndpoint
{
	public record CreateUserRequest(string Name, string Email);
	public record CreateUserResponse(Guid Id, string Name, string Email);

	public async Task<IResult> Handler(AppDbContext context, CreateUserRequest request, CancellationToken cancellationToken)
	{
		if (await context.Users.AnyAsync(u => u.Email == request.Email, cancellationToken))
		{
			return UserErrors.EmailInUse(request.Email).ToProblem();
		}

		var result = await User.Create(request.Name, request.Email)
			.Tap(u => context.Users.Add(u))
			.TapAsync(async _ => await context.SaveChangesAsync(cancellationToken));

		return result
			.Map(u => new CreateUserResponse(u.Id, u.Name, u.Email))
			.ToCreatedAtRouteOrProblem(nameof(GetUserById), r => new { id = r.Id });
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPost("/users", Handler)
			.WithTags("Users")
			.WithName(nameof(CreateUser));
	}
}
