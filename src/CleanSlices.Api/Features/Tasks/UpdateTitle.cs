using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Shared;
#endif
#if (repository)
using CleanSlices.Core.Domain;
#endif
#if (repository || validation)
using CleanSlices.Core.Domain.Tasks;
#endif
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
#endif
#if (validation)
using FluentValidation;
using TaskItemFieldErrors = CleanSlices.Core.Domain.Tasks.TaskItemErrors;
#endif

namespace CleanSlices.Api.Features.Tasks;

public class UpdateTitle : IEndpoint
{
	public record UpdateTaskItemTitleRequest(string Title);
#if (validation)

	public sealed class UpdateTaskItemTitleRequestValidator : AbstractValidator<UpdateTaskItemTitleRequest>
	{
		public UpdateTaskItemTitleRequestValidator()
		{
			RuleFor(r => r.Title).Cascade(CascadeMode.Stop)
				.NotEmpty().WithMessage(TaskItemFieldErrors.EmptyTitle.Description)
				.MaximumLength(TaskItem.TitleMaxLength).WithMessage(TaskItemFieldErrors.TitleTooLong.Description);
		}
	}
#endif

#if (cqrs && result)
	public sealed record UpdateTitleCommand(Guid Id, string Title) : ICommand<Result>;

#elif (cqrs)
	public sealed record UpdateTitleCommand(Guid Id, string Title) : ICommand;

#endif
#if (cqrs && repository && result)
	internal sealed class UpdateTitleHandler(ITaskItemRepository tasks, IUnitOfWork unitOfWork) : ICommandHandler<UpdateTitleCommand, Result>
	{
		public async ValueTask<Result> Handle(UpdateTitleCommand command, CancellationToken cancellationToken)
		{
			var task = await tasks.GetByIdAsync(command.Id, cancellationToken);
			if (task is null)
			{
				return Result.Failure(TaskItemErrors.NotFound(command.Id));
			}

			var result = await task.UpdateTitle(command.Title)
				.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs && repository)
	internal sealed class UpdateTitleHandler(ITaskItemRepository tasks, IUnitOfWork unitOfWork) : ICommandHandler<UpdateTitleCommand>
	{
		public async ValueTask<Unit> Handle(UpdateTitleCommand command, CancellationToken cancellationToken)
		{
			var task = await tasks.GetByIdAsync(command.Id, cancellationToken);
			if (task is null)
			{
				throw TaskItemErrors.NotFound(command.Id);
			}

			task.UpdateTitle(command.Title);
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#elif (cqrs && result)
	internal sealed class UpdateTitleHandler(AppDbContext context) : ICommandHandler<UpdateTitleCommand, Result>
	{
		public async ValueTask<Result> Handle(UpdateTitleCommand command, CancellationToken cancellationToken)
		{
			var task = await context.Tasks.FindAsync([command.Id], cancellationToken);
			if (task is null)
			{
				return Result.Failure(TaskItemErrors.NotFound(command.Id));
			}

			var result = await task.UpdateTitle(command.Title)
				.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs)
	internal sealed class UpdateTitleHandler(AppDbContext context) : ICommandHandler<UpdateTitleCommand>
	{
		public async ValueTask<Unit> Handle(UpdateTitleCommand command, CancellationToken cancellationToken)
		{
			var task = await context.Tasks.FindAsync([command.Id], cancellationToken);
			if (task is null)
			{
				throw TaskItemErrors.NotFound(command.Id);
			}

			task.UpdateTitle(command.Title);
			await context.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, UpdateTaskItemTitleRequest request, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new UpdateTitleCommand(id, request.Title), cancellationToken);
		return result.ToNoContentOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, UpdateTaskItemTitleRequest request, CancellationToken cancellationToken)
	{
		await sender.Send(new UpdateTitleCommand(id, request.Title), cancellationToken);
		return Results.NoContent();
	}
#elif (repository && result)
	public async Task<IResult> Handler(ITaskItemRepository tasks, IUnitOfWork unitOfWork, Guid id, UpdateTaskItemTitleRequest request, CancellationToken cancellationToken)
	{
		var task = await tasks.GetByIdAsync(id, cancellationToken);
		if (task is null)
		{
			return TaskItemErrors.NotFound(id).ToProblem();
		}

		var result = await task.UpdateTitle(request.Title)
			.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}
#elif (repository)
	public async Task<IResult> Handler(ITaskItemRepository tasks, IUnitOfWork unitOfWork, Guid id, UpdateTaskItemTitleRequest request, CancellationToken cancellationToken)
	{
		var task = await tasks.GetByIdAsync(id, cancellationToken);
		if (task is null)
		{
			throw TaskItemErrors.NotFound(id);
		}

		task.UpdateTitle(request.Title);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#elif (result)
	public async Task<IResult> Handler(AppDbContext context, Guid id, UpdateTaskItemTitleRequest request, CancellationToken cancellationToken)
	{
		var task = await context.Tasks.FindAsync([id], cancellationToken);
		if (task is null)
		{
			return TaskItemErrors.NotFound(id).ToProblem();
		}

		var result = await task.UpdateTitle(request.Title)
			.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}
#else
	public async Task<IResult> Handler(AppDbContext context, Guid id, UpdateTaskItemTitleRequest request, CancellationToken cancellationToken)
	{
		var task = await context.Tasks.FindAsync([id], cancellationToken);
		if (task is null)
		{
			throw TaskItemErrors.NotFound(id);
		}

		task.UpdateTitle(request.Title);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/tasks/{id:guid}/title", Handler)
			.WithTags("Tasks")
			.WithName(nameof(UpdateTitle));
	}
}
