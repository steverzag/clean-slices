using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Shared;
#endif
using CleanSlices.Infrastructure.Persistence;
#if (validation)
using CleanSlices.Core.Domain.Tasks;
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

#if (result)
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
