using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Shared;
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
			return TaskItemErrors.NotFound(id).ToProblem();
		}

		var result = await task.UnassignUser(userId)
			.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapDelete("/tasks/{id:guid}/users/{userId:guid}", Handler)
			.WithTags("Tasks")
			.WithName(nameof(UnassignUser));
	}
}
