using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Shared;
#endif
#if (repository)
using CleanSlices.Core.Domain;
using CleanSlices.Core.Domain.Tasks;
#endif
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Tasks;

public class UnassignUser : IEndpoint
{
#if (cqrs && result)
	public sealed record UnassignUserCommand(Guid Id, Guid UserId) : ICommand<Result>;

#elif (cqrs)
	public sealed record UnassignUserCommand(Guid Id, Guid UserId) : ICommand;

#endif
#if (cqrs && repository && result)
	internal sealed class UnassignUserHandler(ITaskItemRepository tasks, IUnitOfWork unitOfWork) : ICommandHandler<UnassignUserCommand, Result>
	{
		public async ValueTask<Result> Handle(UnassignUserCommand command, CancellationToken cancellationToken)
		{
			var task = await tasks.GetWithAssignmentsAsync(command.Id, cancellationToken);
			if (task is null)
			{
				return Result.Failure(TaskItemErrors.NotFound(command.Id));
			}

			var result = await task.UnassignUser(command.UserId)
				.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs && repository)
	internal sealed class UnassignUserHandler(ITaskItemRepository tasks, IUnitOfWork unitOfWork) : ICommandHandler<UnassignUserCommand>
	{
		public async ValueTask<Unit> Handle(UnassignUserCommand command, CancellationToken cancellationToken)
		{
			var task = await tasks.GetWithAssignmentsAsync(command.Id, cancellationToken);
			if (task is null)
			{
				throw TaskItemErrors.NotFound(command.Id);
			}

			task.UnassignUser(command.UserId);
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#elif (cqrs && result)
	internal sealed class UnassignUserHandler(AppDbContext context) : ICommandHandler<UnassignUserCommand, Result>
	{
		public async ValueTask<Result> Handle(UnassignUserCommand command, CancellationToken cancellationToken)
		{
			var task = await context.Tasks
				.Include(t => t.UserAssignments)
				.FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
			if (task is null)
			{
				return Result.Failure(TaskItemErrors.NotFound(command.Id));
			}

			var result = await task.UnassignUser(command.UserId)
				.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs)
	internal sealed class UnassignUserHandler(AppDbContext context) : ICommandHandler<UnassignUserCommand>
	{
		public async ValueTask<Unit> Handle(UnassignUserCommand command, CancellationToken cancellationToken)
		{
			var task = await context.Tasks
				.Include(t => t.UserAssignments)
				.FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
			if (task is null)
			{
				throw TaskItemErrors.NotFound(command.Id);
			}

			task.UnassignUser(command.UserId);
			await context.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, Guid userId, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new UnassignUserCommand(id, userId), cancellationToken);
		return result.ToNoContentOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, Guid userId, CancellationToken cancellationToken)
	{
		await sender.Send(new UnassignUserCommand(id, userId), cancellationToken);
		return Results.NoContent();
	}
#elif (repository && result)
	public async Task<IResult> Handler(ITaskItemRepository tasks, IUnitOfWork unitOfWork, Guid id, Guid userId, CancellationToken cancellationToken)
	{
		var task = await tasks.GetWithAssignmentsAsync(id, cancellationToken);
		if (task is null)
		{
			return TaskItemErrors.NotFound(id).ToProblem();
		}

		var result = await task.UnassignUser(userId)
			.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}
#elif (repository)
	public async Task<IResult> Handler(ITaskItemRepository tasks, IUnitOfWork unitOfWork, Guid id, Guid userId, CancellationToken cancellationToken)
	{
		var task = await tasks.GetWithAssignmentsAsync(id, cancellationToken);
		if (task is null)
		{
			throw TaskItemErrors.NotFound(id);
		}

		task.UnassignUser(userId);
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

		var result = await task.UnassignUser(userId)
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

		task.UnassignUser(userId);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapDelete("/tasks/{id:guid}/users/{userId:guid}", Handler)
			.WithTags("Tasks")
			.WithName(nameof(UnassignUser));
	}
}
