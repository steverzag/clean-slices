using CleanSlices.Api.Configuration;
using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Infrastructure.Pagination;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Tasks;

public class GetTaskItems : IEndpoint
{
	public record TaskItemListItemResponse(Guid Id, string Title, string Description, TaskItemStatus Status, DateTimeOffset CreatedAt);

	public async Task<IResult> Handler(AppDbContext context, [AsParameters] PaginationParameters paging, CancellationToken cancellationToken)
	{
		var tasks = await context.Tasks
			.AsNoTracking()
			.OrderByDescending(t => t.CreatedAt)
			.ThenByDescending(t => t.Id)
			.Select(t => new TaskItemListItemResponse(t.Id, t.Title, t.Description, t.Status, t.CreatedAt))
			.PaginateAsync(paging, cancellationToken);

		return Results.Ok(tasks.ToResponse());
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/tasks", Handler)
			.WithTags("Tasks")
			.WithName(nameof(GetTaskItems));
	}
}
