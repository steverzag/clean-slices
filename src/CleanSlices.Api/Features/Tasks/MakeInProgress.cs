using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
#if (repository)
using CleanSlices.Core.Domain;
using CleanSlices.Core.Domain.Tasks;
#endif
#if (cqrs && result)
using CleanSlices.Core.Shared;
#endif
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
#endif

namespace CleanSlices.Api.Features.Tasks;

public class MakeInProgress : IEndpoint
{
#if (cqrs && result)
	public sealed record MakeInProgressCommand(Guid Id) : ICommand<Result>;

#elif (cqrs)
	public sealed record MakeInProgressCommand(Guid Id) : ICommand;

#endif
#if (cqrs && repository && result)
	internal sealed class MakeInProgressHandler(ITaskItemRepository tasks, IUnitOfWork unitOfWork) : ICommandHandler<MakeInProgressCommand, Result>
	{
		public async ValueTask<Result> Handle(MakeInProgressCommand command, CancellationToken cancellationToken)
		{
			var task = await tasks.GetByIdAsync(command.Id, cancellationToken);
			if (task is null)
			{
				return Result.Failure(TaskItemErrors.NotFound(command.Id));
			}

			task.MakeInProgress();
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Result.Success();
		}
	}
#elif (cqrs && repository)
	internal sealed class MakeInProgressHandler(ITaskItemRepository tasks, IUnitOfWork unitOfWork) : ICommandHandler<MakeInProgressCommand>
	{
		public async ValueTask<Unit> Handle(MakeInProgressCommand command, CancellationToken cancellationToken)
		{
			var task = await tasks.GetByIdAsync(command.Id, cancellationToken);
			if (task is null)
			{
				throw TaskItemErrors.NotFound(command.Id);
			}

			task.MakeInProgress();
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#elif (cqrs && result)
	internal sealed class MakeInProgressHandler(AppDbContext context) : ICommandHandler<MakeInProgressCommand, Result>
	{
		public async ValueTask<Result> Handle(MakeInProgressCommand command, CancellationToken cancellationToken)
		{
			var task = await context.Tasks.FindAsync([command.Id], cancellationToken);
			if (task is null)
			{
				return Result.Failure(TaskItemErrors.NotFound(command.Id));
			}

			task.MakeInProgress();
			await context.SaveChangesAsync(cancellationToken);

			return Result.Success();
		}
	}
#elif (cqrs)
	internal sealed class MakeInProgressHandler(AppDbContext context) : ICommandHandler<MakeInProgressCommand>
	{
		public async ValueTask<Unit> Handle(MakeInProgressCommand command, CancellationToken cancellationToken)
		{
			var task = await context.Tasks.FindAsync([command.Id], cancellationToken);
			if (task is null)
			{
				throw TaskItemErrors.NotFound(command.Id);
			}

			task.MakeInProgress();
			await context.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new MakeInProgressCommand(id), cancellationToken);
		return result.ToNoContentOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken)
	{
		await sender.Send(new MakeInProgressCommand(id), cancellationToken);
		return Results.NoContent();
	}
#elif (repository && result)
	public async Task<IResult> Handler(ITaskItemRepository tasks, IUnitOfWork unitOfWork, Guid id, CancellationToken cancellationToken)
	{
		var task = await tasks.GetByIdAsync(id, cancellationToken);
		if (task is null)
		{
			return TaskItemErrors.NotFound(id).ToProblem();
		}

		task.MakeInProgress();
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#elif (repository)
	public async Task<IResult> Handler(ITaskItemRepository tasks, IUnitOfWork unitOfWork, Guid id, CancellationToken cancellationToken)
	{
		var task = await tasks.GetByIdAsync(id, cancellationToken);
		if (task is null)
		{
			throw TaskItemErrors.NotFound(id);
		}

		task.MakeInProgress();
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#elif (result)
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var task = await context.Tasks.FindAsync([id], cancellationToken);
		if (task is null)
		{
			return TaskItemErrors.NotFound(id).ToProblem();
		}

		task.MakeInProgress();
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#else
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var task = await context.Tasks.FindAsync([id], cancellationToken);
		if (task is null)
		{
			throw TaskItemErrors.NotFound(id);
		}

		task.MakeInProgress();
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/tasks/{id:guid}/in-progress", Handler)
			.WithTags("Tasks")
			.WithName(nameof(MakeInProgress));
	}
}
