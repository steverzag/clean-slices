using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Shared;
#endif
#if (repository)
using CleanSlices.Core.Domain;
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Domain.Users;
#endif
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Tasks;

public class AssignUser : IEndpoint
{
#if (cqrs && result)
	public sealed record AssignUserCommand(Guid Id, Guid UserId) : ICommand<Result>;

#elif (cqrs)
	public sealed record AssignUserCommand(Guid Id, Guid UserId) : ICommand;

#endif
#if (cqrs && repository && result)
	internal sealed class AssignUserHandler(ITaskItemRepository tasks, IUserRepository users, IUnitOfWork unitOfWork) : ICommandHandler<AssignUserCommand, Result>
	{
		public async ValueTask<Result> Handle(AssignUserCommand command, CancellationToken cancellationToken)
		{
			var task = await tasks.GetWithAssignmentsAsync(command.Id, cancellationToken);
			if (task is null)
			{
				return Result.Failure(TaskItemErrors.NotFound(command.Id));
			}

			if (!await users.ExistsAsync(command.UserId, cancellationToken))
			{
				return Result.Failure(TaskItemErrors.UserNotFound(command.UserId));
			}

			var result = await task.AssignUser(command.UserId)
				.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs && repository)
	internal sealed class AssignUserHandler(ITaskItemRepository tasks, IUserRepository users, IUnitOfWork unitOfWork) : ICommandHandler<AssignUserCommand>
	{
		public async ValueTask<Unit> Handle(AssignUserCommand command, CancellationToken cancellationToken)
		{
			var task = await tasks.GetWithAssignmentsAsync(command.Id, cancellationToken);
			if (task is null)
			{
				throw TaskItemErrors.NotFound(command.Id);
			}

			if (!await users.ExistsAsync(command.UserId, cancellationToken))
			{
				throw TaskItemErrors.UserNotFound(command.UserId);
			}

			task.AssignUser(command.UserId);
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#elif (cqrs && result)
	internal sealed class AssignUserHandler(AppDbContext context) : ICommandHandler<AssignUserCommand, Result>
	{
		public async ValueTask<Result> Handle(AssignUserCommand command, CancellationToken cancellationToken)
		{
			var task = await context.Tasks
				.Include(t => t.UserAssignments)
				.FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
			if (task is null)
			{
				return Result.Failure(TaskItemErrors.NotFound(command.Id));
			}

			if (!await context.Users.AnyAsync(u => u.Id == command.UserId, cancellationToken))
			{
				return Result.Failure(TaskItemErrors.UserNotFound(command.UserId));
			}

			var result = await task.AssignUser(command.UserId)
				.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs)
	internal sealed class AssignUserHandler(AppDbContext context) : ICommandHandler<AssignUserCommand>
	{
		public async ValueTask<Unit> Handle(AssignUserCommand command, CancellationToken cancellationToken)
		{
			var task = await context.Tasks
				.Include(t => t.UserAssignments)
				.FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
			if (task is null)
			{
				throw TaskItemErrors.NotFound(command.Id);
			}

			if (!await context.Users.AnyAsync(u => u.Id == command.UserId, cancellationToken))
			{
				throw TaskItemErrors.UserNotFound(command.UserId);
			}

			task.AssignUser(command.UserId);
			await context.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, Guid userId, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new AssignUserCommand(id, userId), cancellationToken);
		return result.ToNoContentOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, Guid userId, CancellationToken cancellationToken)
	{
		await sender.Send(new AssignUserCommand(id, userId), cancellationToken);
		return Results.NoContent();
	}
#elif (repository && result)
	public async Task<IResult> Handler(ITaskItemRepository tasks, IUserRepository users, IUnitOfWork unitOfWork, Guid id, Guid userId, CancellationToken cancellationToken)
	{
		var task = await tasks.GetWithAssignmentsAsync(id, cancellationToken);
		if (task is null)
		{
			return TaskItemErrors.NotFound(id).ToProblem();
		}

		if (!await users.ExistsAsync(userId, cancellationToken))
		{
			return TaskItemErrors.UserNotFound(userId).ToProblem();
		}

		var result = await task.AssignUser(userId)
			.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}
#elif (repository)
	public async Task<IResult> Handler(ITaskItemRepository tasks, IUserRepository users, IUnitOfWork unitOfWork, Guid id, Guid userId, CancellationToken cancellationToken)
	{
		var task = await tasks.GetWithAssignmentsAsync(id, cancellationToken);
		if (task is null)
		{
			throw TaskItemErrors.NotFound(id);
		}

		if (!await users.ExistsAsync(userId, cancellationToken))
		{
			throw TaskItemErrors.UserNotFound(userId);
		}

		task.AssignUser(userId);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#elif (result)
	public async Task<IResult> Handler(AppDbContext context, Guid id, Guid userId, CancellationToken cancellationToken)
	{
		var task = await context.Tasks
			.Include(t => t.UserAssignments)
			.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
		if (task is null)
		{
			return TaskItemErrors.NotFound(id).ToProblem();
		}

		if (!await context.Users.AnyAsync(u => u.Id == userId, cancellationToken))
		{
			return TaskItemErrors.UserNotFound(userId).ToProblem();
		}

		var result = await task.AssignUser(userId)
			.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}
#else
	public async Task<IResult> Handler(AppDbContext context, Guid id, Guid userId, CancellationToken cancellationToken)
	{
		var task = await context.Tasks
			.Include(t => t.UserAssignments)
			.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
		if (task is null)
		{
			throw TaskItemErrors.NotFound(id);
		}

		if (!await context.Users.AnyAsync(u => u.Id == userId, cancellationToken))
		{
			throw TaskItemErrors.UserNotFound(userId);
		}

		task.AssignUser(userId);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPost("/tasks/{id:guid}/users/{userId:guid}", Handler)
			.WithTags("Tasks")
			.WithName(nameof(AssignUser));
	}
}
