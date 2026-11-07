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
#if (repository)
using CleanSlices.Core.Domain;
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

#if (cqrs && result)
	public sealed record CreateUserCommand(string Name, string Email) : ICommand<Result<CreateUserResponse>>;

#elif (cqrs)
	public sealed record CreateUserCommand(string Name, string Email) : ICommand<CreateUserResponse>;

#endif
#if (cqrs && repository && result)
	internal sealed class CreateUserHandler(IUserRepository users, IUnitOfWork unitOfWork) : ICommandHandler<CreateUserCommand, Result<CreateUserResponse>>
	{
		public async ValueTask<Result<CreateUserResponse>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
		{
			if (await users.IsEmailInUseAsync(command.Email, cancellationToken))
			{
				return Result.Failure<CreateUserResponse>(UserErrors.EmailInUse(command.Email));
			}

			var result = await User.Create(command.Name, command.Email)
				.Tap(u => users.Add(u))
				.TapAsync(async _ => await unitOfWork.SaveChangesAsync(cancellationToken));

			return result.Map(u => new CreateUserResponse(u.Id, u.Name, u.Email));
		}
	}
#elif (cqrs && repository)
	internal sealed class CreateUserHandler(IUserRepository users, IUnitOfWork unitOfWork) : ICommandHandler<CreateUserCommand, CreateUserResponse>
	{
		public async ValueTask<CreateUserResponse> Handle(CreateUserCommand command, CancellationToken cancellationToken)
		{
			if (await users.IsEmailInUseAsync(command.Email, cancellationToken))
			{
				throw UserErrors.EmailInUse(command.Email);
			}

			var user = User.Create(command.Name, command.Email);
			users.Add(user);
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return new CreateUserResponse(user.Id, user.Name, user.Email);
		}
	}
#elif (cqrs && result)
	internal sealed class CreateUserHandler(AppDbContext context) : ICommandHandler<CreateUserCommand, Result<CreateUserResponse>>
	{
		public async ValueTask<Result<CreateUserResponse>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
		{
			if (await context.Users.AnyAsync(u => u.Email == command.Email, cancellationToken))
			{
				return Result.Failure<CreateUserResponse>(UserErrors.EmailInUse(command.Email));
			}

			var result = await User.Create(command.Name, command.Email)
				.Tap(u => context.Users.Add(u))
				.TapAsync(async _ => await context.SaveChangesAsync(cancellationToken));

			return result.Map(u => new CreateUserResponse(u.Id, u.Name, u.Email));
		}
	}
#elif (cqrs)
	internal sealed class CreateUserHandler(AppDbContext context) : ICommandHandler<CreateUserCommand, CreateUserResponse>
	{
		public async ValueTask<CreateUserResponse> Handle(CreateUserCommand command, CancellationToken cancellationToken)
		{
			if (await context.Users.AnyAsync(u => u.Email == command.Email, cancellationToken))
			{
				throw UserErrors.EmailInUse(command.Email);
			}

			var user = User.Create(command.Name, command.Email);
			context.Users.Add(user);
			await context.SaveChangesAsync(cancellationToken);

			return new CreateUserResponse(user.Id, user.Name, user.Email);
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, CreateUserRequest request, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new CreateUserCommand(request.Name, request.Email), cancellationToken);
		return result.ToCreatedAtRouteOrProblem(nameof(GetUserById), r => new { id = r.Id });
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, CreateUserRequest request, CancellationToken cancellationToken)
	{
		var response = await sender.Send(new CreateUserCommand(request.Name, request.Email), cancellationToken);
		return Results.CreatedAtRoute(nameof(GetUserById), new { id = response.Id }, response);
	}
#elif (repository && result)
	public async Task<IResult> Handler(IUserRepository users, IUnitOfWork unitOfWork, CreateUserRequest request, CancellationToken cancellationToken)
	{
		if (await users.IsEmailInUseAsync(request.Email, cancellationToken))
		{
			return UserErrors.EmailInUse(request.Email).ToProblem();
		}

		var result = await User.Create(request.Name, request.Email)
			.Tap(u => users.Add(u))
			.TapAsync(async _ => await unitOfWork.SaveChangesAsync(cancellationToken));

		return result
			.Map(u => new CreateUserResponse(u.Id, u.Name, u.Email))
			.ToCreatedAtRouteOrProblem(nameof(GetUserById), r => new { id = r.Id });
	}
#elif (repository)
	public async Task<IResult> Handler(IUserRepository users, IUnitOfWork unitOfWork, CreateUserRequest request, CancellationToken cancellationToken)
	{
		if (await users.IsEmailInUseAsync(request.Email, cancellationToken))
		{
			throw UserErrors.EmailInUse(request.Email);
		}

		var user = User.Create(request.Name, request.Email);
		users.Add(user);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		var response = new CreateUserResponse(user.Id, user.Name, user.Email);
		return Results.CreatedAtRoute(nameof(GetUserById), new { id = response.Id }, response);
	}
#elif (result)
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
