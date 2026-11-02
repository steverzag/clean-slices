using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Api.Extensions;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Tasks;

public class MakeInProgress : IEndpoint
{
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var task = await context.Tasks.FindAsync([id], cancellationToken);
		if (task is null)
		{
			return TaskItemErrors.NotFound(id).ToProblem();
		}

		task.MakeInProgress();
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/tasks/{id:guid}/in-progress", Handler)
			.WithTags("Tasks")
			.WithName(nameof(MakeInProgress));
	}
}
