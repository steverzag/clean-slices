using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Core.Domain.Users;
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
			throw UserErrors.EmailInUse(request.Email);
		}

		var user = User.Create(request.Name, request.Email);
		context.Users.Add(user);
		await context.SaveChangesAsync(cancellationToken);

		var response = new CreateUserResponse(user.Id, user.Name, user.Email);
		return Results.CreatedAtRoute(nameof(GetUserById), new { id = response.Id }, response);
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPost("/users", Handler)
			.WithTags("Users")
			.WithName(nameof(CreateUser));
	}
}
