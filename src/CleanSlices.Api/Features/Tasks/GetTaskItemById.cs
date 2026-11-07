using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
using CleanSlices.Core.Domain.Tasks;
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Tasks;

public class GetTaskItemById : IEndpoint
{
	public record TaskItemResponse(
		Guid Id,
		string Title,
		string Description,
		TaskItemStatus Status,
		DateTimeOffset CreatedAt,
		IReadOnlyList<Guid> UserIds);

#if (repository && result)
	public async Task<IResult> Handler(ITaskItemRepository tasks, Guid id, CancellationToken cancellationToken)
	{
		var task = await tasks.GetWithAssignmentsAsync(id, cancellationToken);

		return task is null
			? TaskItemErrors.NotFound(id).ToProblem()
			: Results.Ok(new TaskItemResponse(
				task.Id,
				task.Title,
				task.Description,
				task.Status,
				task.CreatedAt,
				[.. task.UserAssignments.OrderBy(a => a.AssignedOn).Select(a => a.UserId)]));
	}
#elif (repository)
	public async Task<IResult> Handler(ITaskItemRepository tasks, Guid id, CancellationToken cancellationToken)
	{
		var task = await tasks.GetWithAssignmentsAsync(id, cancellationToken);

		return task is null
			? throw TaskItemErrors.NotFound(id)
			: Results.Ok(new TaskItemResponse(
				task.Id,
				task.Title,
				task.Description,
				task.Status,
				task.CreatedAt,
				[.. task.UserAssignments.OrderBy(a => a.AssignedOn).Select(a => a.UserId)]));
	}
#elif (result)
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var task = await context.Tasks
			.AsNoTracking()
			.Where(t => t.Id == id)
			.Select(t => new TaskItemResponse(
				t.Id,
				t.Title,
				t.Description,
				t.Status,
				t.CreatedAt,
				t.UserAssignments
					.OrderBy(a => a.AssignedOn)
					.Select(a => a.UserId)
					.ToList()))
			.FirstOrDefaultAsync(cancellationToken);

		return task is null
			? TaskItemErrors.NotFound(id).ToProblem()
			: Results.Ok(task);
	}
#else
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var task = await context.Tasks
			.AsNoTracking()
			.Where(t => t.Id == id)
			.Select(t => new TaskItemResponse(
				t.Id,
				t.Title,
				t.Description,
				t.Status,
				t.CreatedAt,
				t.UserAssignments
					.OrderBy(a => a.AssignedOn)
					.Select(a => a.UserId)
					.ToList()))
			.FirstOrDefaultAsync(cancellationToken);

		return task is null
			? throw TaskItemErrors.NotFound(id)
			: Results.Ok(task);
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/tasks/{id:guid}", Handler)
			.WithTags("Tasks")
			.WithName(nameof(GetTaskItemById));
	}
}
