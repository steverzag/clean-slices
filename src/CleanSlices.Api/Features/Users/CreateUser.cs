using CleanSlices.Api.Configuration.Endpoints;
#if (idempotency)
using CleanSlices.Api.Configuration.Idempotency;
#endif
#if (result)
using CleanSlices.Api.Extensions;
#endif
using CleanSlices.Core.Domain.Users;
#if (result)
using CleanSlices.Core.Shared;
#endif
using CleanSlices.Infrastructure.Persistence;
#if (validation)
using FluentValidation;
using UserFieldErrors = CleanSlices.Core.Domain.Users.UserErrors;
#endif
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Users;

public class CreateUser : IEndpoint
{
	public record CreateUserRequest(string Name, string Email);
	public record CreateUserResponse(Guid Id, string Name, string Email);
#if (validation)

	public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
	{
		public CreateUserRequestValidator()
		{
			RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
				.NotEmpty().WithMessage(UserFieldErrors.EmptyName.Description)
				.MaximumLength(User.NameMaxLength).WithMessage(UserFieldErrors.NameTooLong.Description);

			RuleFor(r => r.Email).Cascade(CascadeMode.Stop)
				.NotEmpty().WithMessage(UserFieldErrors.EmptyEmail.Description)
				.MaximumLength(User.EmailMaxLength).WithMessage(UserFieldErrors.EmailTooLong.Description)
				.Must(CleanSlices.Core.Shared.EmailValidator.IsValid).WithMessage(UserFieldErrors.InvalidEmail.Description);
		}
	}
#endif

#if (result)
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
#else
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
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPost("/users", Handler)
			.WithTags("Users")
#if (idempotency)
			.WithIdempotency()
#endif
			.WithName(nameof(CreateUser));
	}
}
