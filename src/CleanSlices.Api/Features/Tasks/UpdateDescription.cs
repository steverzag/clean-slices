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

#if (result)
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
