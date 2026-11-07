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

public class MakeNotStarted : IEndpoint
{
#if (cqrs && result)
	public sealed record MakeNotStartedCommand(Guid Id) : ICommand<Result>;

#elif (cqrs)
	public sealed record MakeNotStartedCommand(Guid Id) : ICommand;

#endif
#if (cqrs && repository && result)
	internal sealed class MakeNotStartedHandler(ITaskItemRepository tasks, IUnitOfWork unitOfWork) : ICommandHandler<MakeNotStartedCommand, Result>
	{
		public async ValueTask<Result> Handle(MakeNotStartedCommand command, CancellationToken cancellationToken)
		{
			var task = await tasks.GetByIdAsync(command.Id, cancellationToken);
			if (task is null)
			{
				return Result.Failure(TaskItemErrors.NotFound(command.Id));
			}

			task.MakeNotStarted();
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Result.Success();
		}
	}
#elif (cqrs && repository)
	internal sealed class MakeNotStartedHandler(ITaskItemRepository tasks, IUnitOfWork unitOfWork) : ICommandHandler<MakeNotStartedCommand>
	{
		public async ValueTask<Unit> Handle(MakeNotStartedCommand command, CancellationToken cancellationToken)
		{
			var task = await tasks.GetByIdAsync(command.Id, cancellationToken);
			if (task is null)
			{
				throw TaskItemErrors.NotFound(command.Id);
			}

			task.MakeNotStarted();
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#elif (cqrs && result)
	internal sealed class MakeNotStartedHandler(AppDbContext context) : ICommandHandler<MakeNotStartedCommand, Result>
	{
		public async ValueTask<Result> Handle(MakeNotStartedCommand command, CancellationToken cancellationToken)
		{
			var task = await context.Tasks.FindAsync([command.Id], cancellationToken);
			if (task is null)
			{
				return Result.Failure(TaskItemErrors.NotFound(command.Id));
			}

			task.MakeNotStarted();
			await context.SaveChangesAsync(cancellationToken);

			return Result.Success();
		}
	}
#elif (cqrs)
	internal sealed class MakeNotStartedHandler(AppDbContext context) : ICommandHandler<MakeNotStartedCommand>
	{
		public async ValueTask<Unit> Handle(MakeNotStartedCommand command, CancellationToken cancellationToken)
		{
			var task = await context.Tasks.FindAsync([command.Id], cancellationToken);
			if (task is null)
			{
				throw TaskItemErrors.NotFound(command.Id);
			}

			task.MakeNotStarted();
			await context.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new MakeNotStartedCommand(id), cancellationToken);
		return result.ToNoContentOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken)
	{
		await sender.Send(new MakeNotStartedCommand(id), cancellationToken);
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

		task.MakeNotStarted();
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

		task.MakeNotStarted();
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

		task.MakeNotStarted();
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

		task.MakeNotStarted();
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/tasks/{id:guid}/not-started", Handler)
			.WithTags("Tasks")
			.WithName(nameof(MakeNotStarted));
	}
}
