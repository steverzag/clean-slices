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

public class UpdateDescription : IEndpoint
{
	public record UpdateTaskItemDescriptionRequest(string Description);
#if (validation)

	public sealed class UpdateTaskItemDescriptionRequestValidator : AbstractValidator<UpdateTaskItemDescriptionRequest>
	{
		public UpdateTaskItemDescriptionRequestValidator()
		{
			RuleFor(r => r.Description).Cascade(CascadeMode.Stop)
				.NotEmpty().WithMessage(TaskItemFieldErrors.EmptyDescription.Description)
				.MaximumLength(TaskItem.DescriptionMaxLength).WithMessage(TaskItemFieldErrors.DescriptionTooLong.Description);
		}
	}
#endif

#if (cqrs && result)
	public sealed record UpdateDescriptionCommand(Guid Id, string Description) : ICommand<Result>;

#elif (cqrs)
	public sealed record UpdateDescriptionCommand(Guid Id, string Description) : ICommand;

#endif
#if (cqrs && repository && result)
	internal sealed class UpdateDescriptionHandler(ITaskItemRepository tasks, IUnitOfWork unitOfWork) : ICommandHandler<UpdateDescriptionCommand, Result>
	{
		public async ValueTask<Result> Handle(UpdateDescriptionCommand command, CancellationToken cancellationToken)
		{
			var task = await tasks.GetByIdAsync(command.Id, cancellationToken);
			if (task is null)
			{
				return Result.Failure(TaskItemErrors.NotFound(command.Id));
			}

			var result = await task.UpdateDescription(command.Description)
				.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs && repository)
	internal sealed class UpdateDescriptionHandler(ITaskItemRepository tasks, IUnitOfWork unitOfWork) : ICommandHandler<UpdateDescriptionCommand>
	{
		public async ValueTask<Unit> Handle(UpdateDescriptionCommand command, CancellationToken cancellationToken)
		{
			var task = await tasks.GetByIdAsync(command.Id, cancellationToken);
			if (task is null)
			{
				throw TaskItemErrors.NotFound(command.Id);
			}

			task.UpdateDescription(command.Description);
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#elif (cqrs && result)
	internal sealed class UpdateDescriptionHandler(AppDbContext context) : ICommandHandler<UpdateDescriptionCommand, Result>
	{
		public async ValueTask<Result> Handle(UpdateDescriptionCommand command, CancellationToken cancellationToken)
		{
			var task = await context.Tasks.FindAsync([command.Id], cancellationToken);
			if (task is null)
			{
				return Result.Failure(TaskItemErrors.NotFound(command.Id));
			}

			var result = await task.UpdateDescription(command.Description)
				.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs)
	internal sealed class UpdateDescriptionHandler(AppDbContext context) : ICommandHandler<UpdateDescriptionCommand>
	{
		public async ValueTask<Unit> Handle(UpdateDescriptionCommand command, CancellationToken cancellationToken)
		{
			var task = await context.Tasks.FindAsync([command.Id], cancellationToken);
			if (task is null)
			{
				throw TaskItemErrors.NotFound(command.Id);
			}

			task.UpdateDescription(command.Description);
			await context.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, UpdateTaskItemDescriptionRequest request, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new UpdateDescriptionCommand(id, request.Description), cancellationToken);
		return result.ToNoContentOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, UpdateTaskItemDescriptionRequest request, CancellationToken cancellationToken)
	{
		await sender.Send(new UpdateDescriptionCommand(id, request.Description), cancellationToken);
		return Results.NoContent();
	}
#elif (repository && result)
	public async Task<IResult> Handler(ITaskItemRepository tasks, IUnitOfWork unitOfWork, Guid id, UpdateTaskItemDescriptionRequest request, CancellationToken cancellationToken)
	{
		var task = await tasks.GetByIdAsync(id, cancellationToken);
		if (task is null)
		{
			return TaskItemErrors.NotFound(id).ToProblem();
		}

		var result = await task.UpdateDescription(request.Description)
			.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}
#elif (repository)
	public async Task<IResult> Handler(ITaskItemRepository tasks, IUnitOfWork unitOfWork, Guid id, UpdateTaskItemDescriptionRequest request, CancellationToken cancellationToken)
	{
		var task = await tasks.GetByIdAsync(id, cancellationToken);
		if (task is null)
		{
			throw TaskItemErrors.NotFound(id);
		}

		task.UpdateDescription(request.Description);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#elif (result)
	public async Task<IResult> Handler(AppDbContext context, Guid id, UpdateTaskItemDescriptionRequest request, CancellationToken cancellationToken)
	{
		var task = await context.Tasks.FindAsync([id], cancellationToken);
		if (task is null)
		{
			return TaskItemErrors.NotFound(id).ToProblem();
		}

		var result = await task.UpdateDescription(request.Description)
			.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}
#else
	public async Task<IResult> Handler(AppDbContext context, Guid id, UpdateTaskItemDescriptionRequest request, CancellationToken cancellationToken)
	{
		var task = await context.Tasks.FindAsync([id], cancellationToken);
		if (task is null)
		{
			throw TaskItemErrors.NotFound(id);
		}

		task.UpdateDescription(request.Description);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/tasks/{id:guid}/description", Handler)
			.WithTags("Tasks")
			.WithName(nameof(UpdateDescription));
	}
}
