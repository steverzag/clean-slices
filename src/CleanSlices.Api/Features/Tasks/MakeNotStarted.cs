using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Tasks;

public class MakeNotStarted : IEndpoint
{
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var task = await context.Tasks.FindAsync([id], cancellationToken);
		if (task is null)
		{
#if (result)
			return TaskItemErrors.NotFound(id).ToProblem();
#else
			throw TaskItemErrors.NotFound(id);
#endif
		}

		task.MakeNotStarted();
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/tasks/{id:guid}/not-started", Handler)
			.WithTags("Tasks")
			.WithName(nameof(MakeNotStarted));
	}
}
