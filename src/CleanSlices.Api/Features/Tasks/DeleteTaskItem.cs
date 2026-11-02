using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Api.Extensions;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Tasks;

public class DeleteTaskItem : IEndpoint
{
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var task = await context.Tasks.FindAsync([id], cancellationToken);
		if (task is null)
		{
			return TaskItemErrors.NotFound(id).ToProblem();
		}

		task.Delete();
		context.Tasks.Remove(task);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapDelete("/tasks/{id:guid}", Handler)
			.WithTags("Tasks")
			.WithName(nameof(DeleteTaskItem));
	}
}
