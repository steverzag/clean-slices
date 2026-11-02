using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Shared;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Tasks;

public class UpdateDescription : IEndpoint
{
	public record UpdateTaskItemDescriptionRequest(string Description);

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

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/tasks/{id:guid}/description", Handler)
			.WithTags("Tasks")
			.WithName(nameof(UpdateDescription));
	}
}
