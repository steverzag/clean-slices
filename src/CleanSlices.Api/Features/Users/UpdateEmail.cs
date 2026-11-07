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
#if (!repository)
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Users;

public class UpdateEmail : IEndpoint
{
	public record UpdateUserEmailRequest(string Email);
#if (validation)

	public sealed class UpdateUserEmailRequestValidator : AbstractValidator<UpdateUserEmailRequest>
	{
		public UpdateUserEmailRequestValidator()
		{
			RuleFor(r => r.Email).Cascade(CascadeMode.Stop)
				.NotEmpty().WithMessage(UserFieldErrors.EmptyEmail.Description)
				.MaximumLength(User.EmailMaxLength).WithMessage(UserFieldErrors.EmailTooLong.Description)
				.Must(CleanSlices.Core.Shared.EmailValidator.IsValid).WithMessage(UserFieldErrors.InvalidEmail.Description);
		}
	}
#endif

#if (cqrs && result)
	public sealed record UpdateEmailCommand(Guid Id, string Email) : ICommand<Result>;

#elif (cqrs)
	public sealed record UpdateEmailCommand(Guid Id, string Email) : ICommand;

#endif
#if (cqrs && repository && result)
	internal sealed class UpdateEmailHandler(IUserRepository users, IUnitOfWork unitOfWork) : ICommandHandler<UpdateEmailCommand, Result>
	{
		public async ValueTask<Result> Handle(UpdateEmailCommand command, CancellationToken cancellationToken)
		{
			var user = await users.GetByIdAsync(command.Id, cancellationToken);
			if (user is null)
			{
				return Result.Failure(UserErrors.NotFound(command.Id));
			}

			if (await users.IsEmailInUseAsync(command.Email, command.Id, cancellationToken))
			{
				return Result.Failure(UserErrors.EmailInUse(command.Email));
			}

			var result = await user.UpdateEmail(command.Email)
				.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs && repository)
	internal sealed class UpdateEmailHandler(IUserRepository users, IUnitOfWork unitOfWork) : ICommandHandler<UpdateEmailCommand>
	{
		public async ValueTask<Unit> Handle(UpdateEmailCommand command, CancellationToken cancellationToken)
		{
			var user = await users.GetByIdAsync(command.Id, cancellationToken);
			if (user is null)
			{
				throw UserErrors.NotFound(command.Id);
			}

			if (await users.IsEmailInUseAsync(command.Email, command.Id, cancellationToken))
			{
				throw UserErrors.EmailInUse(command.Email);
			}

			user.UpdateEmail(command.Email);
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#elif (cqrs && result)
	internal sealed class UpdateEmailHandler(AppDbContext context) : ICommandHandler<UpdateEmailCommand, Result>
	{
		public async ValueTask<Result> Handle(UpdateEmailCommand command, CancellationToken cancellationToken)
		{
			var user = await context.Users.FindAsync([command.Id], cancellationToken);
			if (user is null)
			{
				return Result.Failure(UserErrors.NotFound(command.Id));
			}

			if (await context.Users.AnyAsync(u => u.Email == command.Email && u.Id != command.Id, cancellationToken))
			{
				return Result.Failure(UserErrors.EmailInUse(command.Email));
			}

			var result = await user.UpdateEmail(command.Email)
				.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs)
	internal sealed class UpdateEmailHandler(AppDbContext context) : ICommandHandler<UpdateEmailCommand>
	{
		public async ValueTask<Unit> Handle(UpdateEmailCommand command, CancellationToken cancellationToken)
		{
			var user = await context.Users.FindAsync([command.Id], cancellationToken);
			if (user is null)
			{
				throw UserErrors.NotFound(command.Id);
			}

			if (await context.Users.AnyAsync(u => u.Email == command.Email && u.Id != command.Id, cancellationToken))
			{
				throw UserErrors.EmailInUse(command.Email);
			}

			user.UpdateEmail(command.Email);
			await context.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, UpdateUserEmailRequest request, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new UpdateEmailCommand(id, request.Email), cancellationToken);
		return result.ToNoContentOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, UpdateUserEmailRequest request, CancellationToken cancellationToken)
	{
		await sender.Send(new UpdateEmailCommand(id, request.Email), cancellationToken);
		return Results.NoContent();
	}
#elif (repository && result)
	public async Task<IResult> Handler(IUserRepository users, IUnitOfWork unitOfWork, Guid id, UpdateUserEmailRequest request, CancellationToken cancellationToken)
	{
		var user = await users.GetByIdAsync(id, cancellationToken);
		if (user is null)
		{
			return UserErrors.NotFound(id).ToProblem();
		}

		if (await users.IsEmailInUseAsync(request.Email, id, cancellationToken))
		{
			return UserErrors.EmailInUse(request.Email).ToProblem();
		}

		var result = await user.UpdateEmail(request.Email)
			.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}
#elif (repository)
	public async Task<IResult> Handler(IUserRepository users, IUnitOfWork unitOfWork, Guid id, UpdateUserEmailRequest request, CancellationToken cancellationToken)
	{
		var user = await users.GetByIdAsync(id, cancellationToken);
		if (user is null)
		{
			throw UserErrors.NotFound(id);
		}

		if (await users.IsEmailInUseAsync(request.Email, id, cancellationToken))
		{
			throw UserErrors.EmailInUse(request.Email);
		}

		user.UpdateEmail(request.Email);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#elif (result)
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
