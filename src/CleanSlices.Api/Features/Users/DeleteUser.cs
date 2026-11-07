using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
#if (repository)
using CleanSlices.Core.Domain;
using CleanSlices.Core.Domain.Users;
#endif
#if (cqrs && result)
using CleanSlices.Core.Shared;
#endif
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
#endif

namespace CleanSlices.Api.Features.Users;

public class DeleteUser : IEndpoint
{
#if (cqrs && result)
	public sealed record DeleteUserCommand(Guid Id) : ICommand<Result>;

#elif (cqrs)
	public sealed record DeleteUserCommand(Guid Id) : ICommand;

#endif
#if (cqrs && repository && result)
	internal sealed class DeleteUserHandler(IUserRepository users, IUnitOfWork unitOfWork) : ICommandHandler<DeleteUserCommand, Result>
	{
		public async ValueTask<Result> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
		{
			var user = await users.GetByIdAsync(command.Id, cancellationToken);
			if (user is null)
			{
				return Result.Failure(UserErrors.NotFound(command.Id));
			}

			user.Delete();
			users.Remove(user);
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Result.Success();
		}
	}
#elif (cqrs && repository)
	internal sealed class DeleteUserHandler(IUserRepository users, IUnitOfWork unitOfWork) : ICommandHandler<DeleteUserCommand>
	{
		public async ValueTask<Unit> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
		{
			var user = await users.GetByIdAsync(command.Id, cancellationToken);
			if (user is null)
			{
				throw UserErrors.NotFound(command.Id);
			}

			user.Delete();
			users.Remove(user);
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#elif (cqrs && result)
	internal sealed class DeleteUserHandler(AppDbContext context) : ICommandHandler<DeleteUserCommand, Result>
	{
		public async ValueTask<Result> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
		{
			var user = await context.Users.FindAsync([command.Id], cancellationToken);
			if (user is null)
			{
				return Result.Failure(UserErrors.NotFound(command.Id));
			}

			user.Delete();
			context.Users.Remove(user);
			await context.SaveChangesAsync(cancellationToken);

			return Result.Success();
		}
	}
#elif (cqrs)
	internal sealed class DeleteUserHandler(AppDbContext context) : ICommandHandler<DeleteUserCommand>
	{
		public async ValueTask<Unit> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
		{
			var user = await context.Users.FindAsync([command.Id], cancellationToken);
			if (user is null)
			{
				throw UserErrors.NotFound(command.Id);
			}

			user.Delete();
			context.Users.Remove(user);
			await context.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new DeleteUserCommand(id), cancellationToken);
		return result.ToNoContentOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken)
	{
		await sender.Send(new DeleteUserCommand(id), cancellationToken);
		return Results.NoContent();
	}
#elif (repository && result)
	public async Task<IResult> Handler(IUserRepository users, IUnitOfWork unitOfWork, Guid id, CancellationToken cancellationToken)
	{
		var user = await users.GetByIdAsync(id, cancellationToken);
		if (user is null)
		{
			return UserErrors.NotFound(id).ToProblem();
		}

		user.Delete();
		users.Remove(user);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#elif (repository)
	public async Task<IResult> Handler(IUserRepository users, IUnitOfWork unitOfWork, Guid id, CancellationToken cancellationToken)
	{
		var user = await users.GetByIdAsync(id, cancellationToken);
		if (user is null)
		{
			throw UserErrors.NotFound(id);
		}

		user.Delete();
		users.Remove(user);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#elif (result)
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var user = await context.Users.FindAsync([id], cancellationToken);
		if (user is null)
		{
			return UserErrors.NotFound(id).ToProblem();
		}

		user.Delete();
		context.Users.Remove(user);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#else
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var user = await context.Users.FindAsync([id], cancellationToken);
		if (user is null)
		{
			throw UserErrors.NotFound(id);
		}

		user.Delete();
		context.Users.Remove(user);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapDelete("/users/{id:guid}", Handler)
			.WithTags("Users")
			.WithName(nameof(DeleteUser));
	}
}
