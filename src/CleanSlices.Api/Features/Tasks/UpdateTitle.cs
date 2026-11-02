using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Tasks;

public class UpdateTitle : IEndpoint
{
	public record UpdateTaskItemTitleRequest(string Title);

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

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/tasks/{id:guid}/title", Handler)
			.WithTags("Tasks")
			.WithName(nameof(UpdateTitle));
	}
}
