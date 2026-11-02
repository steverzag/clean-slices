using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Tasks;

public class GetTaskItems : IEndpoint
{
	public record TaskItemListItemResponse(Guid Id, string Title, string Description, TaskItemStatus Status, DateTimeOffset CreatedAt);

	public async Task<IResult> Handler(AppDbContext context, CancellationToken cancellationToken)
	{
		var tasks = await context.Tasks
			.AsNoTracking()
			.Select(t => new TaskItemListItemResponse(t.Id, t.Title, t.Description, t.Status, t.CreatedAt))
			.ToListAsync(cancellationToken);

		return Results.Ok(tasks);
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/tasks", Handler)
			.WithTags("Tasks")
			.WithName(nameof(GetTaskItems));
	}
}
