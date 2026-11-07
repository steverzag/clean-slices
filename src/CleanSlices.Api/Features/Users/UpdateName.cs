using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Shared;
#endif
using CleanSlices.Infrastructure.Persistence;
#if (validation)
using CleanSlices.Core.Domain.Users;
using FluentValidation;
using UserFieldErrors = CleanSlices.Core.Domain.Users.UserErrors;
#endif

namespace CleanSlices.Api.Features.Users;

public class UpdateName : IEndpoint
{
	public record UpdateUserNameRequest(string Name);
#if (validation)

	public sealed class UpdateUserNameRequestValidator : AbstractValidator<UpdateUserNameRequest>
	{
		public UpdateUserNameRequestValidator()
		{
			RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
				.NotEmpty().WithMessage(UserFieldErrors.EmptyName.Description)
				.MaximumLength(User.NameMaxLength).WithMessage(UserFieldErrors.NameTooLong.Description);
		}
	}
#endif

#if (result)
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
#else
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
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/users/{id:guid}/name", Handler)
			.WithTags("Users")
			.WithName(nameof(UpdateName));
	}
}
