using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Tasks;

public class UnassignUser : IEndpoint
{
	public async Task<IResult> Handler(AppDbContext context, Guid id, Guid userId, CancellationToken cancellationToken)
	{
		var task = await context.Tasks
			.Include(t => t.UserAssignments)
			.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
		if (task is null)
		{
			throw TaskItemErrors.NotFound(id);
		}

		task.UnassignUser(userId);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapDelete("/tasks/{id:guid}/users/{userId:guid}", Handler)
			.WithTags("Tasks")
			.WithName(nameof(UnassignUser));
	}
}
