using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
using CleanSlices.Core.Domain.Tasks;
#if (result)
using CleanSlices.Core.Shared;
#endif
using CleanSlices.Infrastructure.Persistence;
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

	public async Task<IResult> Handler(AppDbContext context, CreateTaskItemRequest request, CancellationToken cancellationToken)
	{
#if (result)
		var result = await TaskItem.Create(request.Title, request.Description)
			.Tap(t => context.Tasks.Add(t))
			.TapAsync(async _ => await context.SaveChangesAsync(cancellationToken));

		return result
			.Map(t => new CreateTaskItemResponse(t.Id, t.Title, t.Description, t.Status, t.CreatedAt))
			.ToCreatedAtRouteOrProblem(nameof(GetTaskItemById), r => new { id = r.Id });
#else
		var task = TaskItem.Create(request.Title, request.Description);
		context.Tasks.Add(task);
		await context.SaveChangesAsync(cancellationToken);

		var response = new CreateTaskItemResponse(task.Id, task.Title, task.Description, task.Status, task.CreatedAt);
		return Results.CreatedAtRoute(nameof(GetTaskItemById), new { id = response.Id }, response);
#endif
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPost("/tasks", Handler)
			.WithTags("Tasks")
			.WithName(nameof(CreateTaskItem));
	}
}
