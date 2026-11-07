using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Shared;
#endif
#if (repository)
using CleanSlices.Core.Domain;
#endif
#if (repository || validation)
using CleanSlices.Core.Domain.Users;
#endif
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
#endif
#if (validation)
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

#if (cqrs && result)
	public sealed record UpdateNameCommand(Guid Id, string Name) : ICommand<Result>;

#elif (cqrs)
	public sealed record UpdateNameCommand(Guid Id, string Name) : ICommand;

#endif
#if (cqrs && repository && result)
	internal sealed class UpdateNameHandler(IUserRepository users, IUnitOfWork unitOfWork) : ICommandHandler<UpdateNameCommand, Result>
	{
		public async ValueTask<Result> Handle(UpdateNameCommand command, CancellationToken cancellationToken)
		{
			var user = await users.GetByIdAsync(command.Id, cancellationToken);
			if (user is null)
			{
				return Result.Failure(UserErrors.NotFound(command.Id));
			}

			var result = await user.UpdateName(command.Name)
				.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs && repository)
	internal sealed class UpdateNameHandler(IUserRepository users, IUnitOfWork unitOfWork) : ICommandHandler<UpdateNameCommand>
	{
		public async ValueTask<Unit> Handle(UpdateNameCommand command, CancellationToken cancellationToken)
		{
			var user = await users.GetByIdAsync(command.Id, cancellationToken);
			if (user is null)
			{
				throw UserErrors.NotFound(command.Id);
			}

			user.UpdateName(command.Name);
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#elif (cqrs && result)
	internal sealed class UpdateNameHandler(AppDbContext context) : ICommandHandler<UpdateNameCommand, Result>
	{
		public async ValueTask<Result> Handle(UpdateNameCommand command, CancellationToken cancellationToken)
		{
			var user = await context.Users.FindAsync([command.Id], cancellationToken);
			if (user is null)
			{
				return Result.Failure(UserErrors.NotFound(command.Id));
			}

			var result = await user.UpdateName(command.Name)
				.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs)
	internal sealed class UpdateNameHandler(AppDbContext context) : ICommandHandler<UpdateNameCommand>
	{
		public async ValueTask<Unit> Handle(UpdateNameCommand command, CancellationToken cancellationToken)
		{
			var user = await context.Users.FindAsync([command.Id], cancellationToken);
			if (user is null)
			{
				throw UserErrors.NotFound(command.Id);
			}

			user.UpdateName(command.Name);
			await context.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, UpdateUserNameRequest request, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new UpdateNameCommand(id, request.Name), cancellationToken);
		return result.ToNoContentOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, UpdateUserNameRequest request, CancellationToken cancellationToken)
	{
		await sender.Send(new UpdateNameCommand(id, request.Name), cancellationToken);
		return Results.NoContent();
	}
#elif (repository && result)
	public async Task<IResult> Handler(IUserRepository users, IUnitOfWork unitOfWork, Guid id, UpdateUserNameRequest request, CancellationToken cancellationToken)
	{
		var user = await users.GetByIdAsync(id, cancellationToken);
		if (user is null)
		{
			return UserErrors.NotFound(id).ToProblem();
		}

		var result = await user.UpdateName(request.Name)
			.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}
#elif (repository)
	public async Task<IResult> Handler(IUserRepository users, IUnitOfWork unitOfWork, Guid id, UpdateUserNameRequest request, CancellationToken cancellationToken)
	{
		var user = await users.GetByIdAsync(id, cancellationToken);
		if (user is null)
		{
			throw UserErrors.NotFound(id);
		}

		user.UpdateName(request.Name);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#elif (result)
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
