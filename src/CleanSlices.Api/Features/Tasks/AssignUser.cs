using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Shared;
#endif
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Tasks;

public class AssignUser : IEndpoint
{
#if (result)
	public async Task<IResult> Handler(AppDbContext context, Guid id, Guid userId, CancellationToken cancellationToken)
	{
		var task = await context.Tasks
			.Include(t => t.UserAssignments)
			.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
		if (task is null)
		{
			return TaskItemErrors.NotFound(id).ToProblem();
		}

		if (!await context.Users.AnyAsync(u => u.Id == userId, cancellationToken))
		{
			return TaskItemErrors.UserNotFound(userId).ToProblem();
		}

		var result = await task.AssignUser(userId)
			.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}
#else
	public async Task<IResult> Handler(AppDbContext context, Guid id, Guid userId, CancellationToken cancellationToken)
	{
		var task = await context.Tasks
			.Include(t => t.UserAssignments)
			.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
		if (task is null)
		{
			throw TaskItemErrors.NotFound(id);
		}

		if (!await context.Users.AnyAsync(u => u.Id == userId, cancellationToken))
		{
			throw TaskItemErrors.UserNotFound(userId);
		}

		task.AssignUser(userId);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPost("/tasks/{id:guid}/users/{userId:guid}", Handler)
			.WithTags("Tasks")
			.WithName(nameof(AssignUser));
	}
}
