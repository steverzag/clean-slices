using CleanSlices.Api.Configuration.Endpoints;
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
			throw TaskItemErrors.NotFound(id);
		}

		task.UpdateDescription(request.Description);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/tasks/{id:guid}/description", Handler)
			.WithTags("Tasks")
			.WithName(nameof(UpdateDescription));
	}
}
