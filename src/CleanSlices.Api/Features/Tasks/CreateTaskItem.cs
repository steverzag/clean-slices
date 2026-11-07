using CleanSlices.Api.Configuration.Endpoints;
#if (idempotency)
using CleanSlices.Api.Configuration.Idempotency;
#endif
#if (result)
using CleanSlices.Api.Extensions;
#endif
using CleanSlices.Core.Domain.Tasks;
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
using TaskItemFieldErrors = CleanSlices.Core.Domain.Tasks.TaskItemErrors;
#endif

namespace CleanSlices.Api.Features.Tasks;

public class CreateTaskItem : IEndpoint
{
	public record CreateTaskItemRequest(string Title, string Description);
	public record CreateTaskItemResponse(Guid Id, string Title, string Description, TaskItemStatus Status, DateTimeOffset CreatedAt);
#if (validation)

	public sealed class CreateTaskItemRequestValidator : AbstractValidator<CreateTaskItemRequest>
	{
		public CreateTaskItemRequestValidator()
		{
			RuleFor(r => r.Title).Cascade(CascadeMode.Stop)
				.NotEmpty().WithMessage(TaskItemFieldErrors.EmptyTitle.Description)
				.MaximumLength(TaskItem.TitleMaxLength).WithMessage(TaskItemFieldErrors.TitleTooLong.Description);

			RuleFor(r => r.Description).Cascade(CascadeMode.Stop)
				.NotEmpty().WithMessage(TaskItemFieldErrors.EmptyDescription.Description)
				.MaximumLength(TaskItem.DescriptionMaxLength).WithMessage(TaskItemFieldErrors.DescriptionTooLong.Description);
		}
	}
#endif

#if (cqrs && result)
	public sealed record CreateTaskItemCommand(string Title, string Description) : ICommand<Result<CreateTaskItemResponse>>;

#elif (cqrs)
	public sealed record CreateTaskItemCommand(string Title, string Description) : ICommand<CreateTaskItemResponse>;

#endif
#if (cqrs && repository && result)
	internal sealed class CreateTaskItemHandler(ITaskItemRepository tasks, IUnitOfWork unitOfWork) : ICommandHandler<CreateTaskItemCommand, Result<CreateTaskItemResponse>>
	{
		public async ValueTask<Result<CreateTaskItemResponse>> Handle(CreateTaskItemCommand command, CancellationToken cancellationToken)
		{
			var result = await TaskItem.Create(command.Title, command.Description)
				.Tap(t => tasks.Add(t))
				.TapAsync(async _ => await unitOfWork.SaveChangesAsync(cancellationToken));

			return result.Map(t => new CreateTaskItemResponse(t.Id, t.Title, t.Description, t.Status, t.CreatedAt));
		}
	}
#elif (cqrs && repository)
	internal sealed class CreateTaskItemHandler(ITaskItemRepository tasks, IUnitOfWork unitOfWork) : ICommandHandler<CreateTaskItemCommand, CreateTaskItemResponse>
	{
		public async ValueTask<CreateTaskItemResponse> Handle(CreateTaskItemCommand command, CancellationToken cancellationToken)
		{
			var task = TaskItem.Create(command.Title, command.Description);
			tasks.Add(task);
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return new CreateTaskItemResponse(task.Id, task.Title, task.Description, task.Status, task.CreatedAt);
		}
	}
#elif (cqrs && result)
	internal sealed class CreateTaskItemHandler(AppDbContext context) : ICommandHandler<CreateTaskItemCommand, Result<CreateTaskItemResponse>>
	{
		public async ValueTask<Result<CreateTaskItemResponse>> Handle(CreateTaskItemCommand command, CancellationToken cancellationToken)
		{
			var result = await TaskItem.Create(command.Title, command.Description)
				.Tap(t => context.Tasks.Add(t))
				.TapAsync(async _ => await context.SaveChangesAsync(cancellationToken));

			return result.Map(t => new CreateTaskItemResponse(t.Id, t.Title, t.Description, t.Status, t.CreatedAt));
		}
	}
#elif (cqrs)
	internal sealed class CreateTaskItemHandler(AppDbContext context) : ICommandHandler<CreateTaskItemCommand, CreateTaskItemResponse>
	{
		public async ValueTask<CreateTaskItemResponse> Handle(CreateTaskItemCommand command, CancellationToken cancellationToken)
		{
			var task = TaskItem.Create(command.Title, command.Description);
			context.Tasks.Add(task);
			await context.SaveChangesAsync(cancellationToken);

			return new CreateTaskItemResponse(task.Id, task.Title, task.Description, task.Status, task.CreatedAt);
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, CreateTaskItemRequest request, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new CreateTaskItemCommand(request.Title, request.Description), cancellationToken);
		return result.ToCreatedAtRouteOrProblem(nameof(GetTaskItemById), r => new { id = r.Id });
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, CreateTaskItemRequest request, CancellationToken cancellationToken)
	{
		var response = await sender.Send(new CreateTaskItemCommand(request.Title, request.Description), cancellationToken);
		return Results.CreatedAtRoute(nameof(GetTaskItemById), new { id = response.Id }, response);
	}
#elif (repository && result)
	public async Task<IResult> Handler(ITaskItemRepository tasks, IUnitOfWork unitOfWork, CreateTaskItemRequest request, CancellationToken cancellationToken)
	{
		var result = await TaskItem.Create(request.Title, request.Description)
			.Tap(t => tasks.Add(t))
			.TapAsync(async _ => await unitOfWork.SaveChangesAsync(cancellationToken));

		return result
			.Map(t => new CreateTaskItemResponse(t.Id, t.Title, t.Description, t.Status, t.CreatedAt))
			.ToCreatedAtRouteOrProblem(nameof(GetTaskItemById), r => new { id = r.Id });
	}
#elif (repository)
	public async Task<IResult> Handler(ITaskItemRepository tasks, IUnitOfWork unitOfWork, CreateTaskItemRequest request, CancellationToken cancellationToken)
	{
		var task = TaskItem.Create(request.Title, request.Description);
		tasks.Add(task);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		var response = new CreateTaskItemResponse(task.Id, task.Title, task.Description, task.Status, task.CreatedAt);
		return Results.CreatedAtRoute(nameof(GetTaskItemById), new { id = response.Id }, response);
	}
#elif (result)
	public async Task<IResult> Handler(AppDbContext context, CreateTaskItemRequest request, CancellationToken cancellationToken)
	{
		var result = await TaskItem.Create(request.Title, request.Description)
			.Tap(t => context.Tasks.Add(t))
			.TapAsync(async _ => await context.SaveChangesAsync(cancellationToken));

		return result
			.Map(t => new CreateTaskItemResponse(t.Id, t.Title, t.Description, t.Status, t.CreatedAt))
			.ToCreatedAtRouteOrProblem(nameof(GetTaskItemById), r => new { id = r.Id });
	}
#else
	public async Task<IResult> Handler(AppDbContext context, CreateTaskItemRequest request, CancellationToken cancellationToken)
	{
		var task = TaskItem.Create(request.Title, request.Description);
		context.Tasks.Add(task);
		await context.SaveChangesAsync(cancellationToken);

		var response = new CreateTaskItemResponse(task.Id, task.Title, task.Description, task.Status, task.CreatedAt);
		return Results.CreatedAtRoute(nameof(GetTaskItemById), new { id = response.Id }, response);
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPost("/tasks", Handler)
			.WithTags("Tasks")
#if (idempotency)
			.WithIdempotency()
#endif
			.WithName(nameof(CreateTaskItem));
	}
}
