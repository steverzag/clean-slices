using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
#if (result)
			? TaskItemErrors.NotFound(id).ToProblem()
#else
			? throw TaskItemErrors.NotFound(id)
#endif
			: Results.Ok(task);
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/tasks/{id:guid}", Handler)
			.WithTags("Tasks")
			.WithName(nameof(GetTaskItemById));
	}
}
