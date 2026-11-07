#if (!repository)
using CleanSlices.Api.Configuration;
#endif
using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Infrastructure.Pagination;
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Tasks;

public class GetTaskItems : IEndpoint
{
	public record TaskItemListItemResponse(Guid Id, string Title, string Description, TaskItemStatus Status, DateTimeOffset CreatedAt);

#if (repository)
	public async Task<IResult> Handler(ITaskItemRepository tasks, [AsParameters] PaginationParameters paging, CancellationToken cancellationToken)
	{
		var page = await tasks.GetPageAsync(paging.PageNumber, paging.PageSize, cancellationToken);

		return Results.Ok(page.Map(t => new TaskItemListItemResponse(t.Id, t.Title, t.Description, t.Status, t.CreatedAt)));
	}
#else
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
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/tasks", Handler)
			.WithTags("Tasks")
			.WithName(nameof(GetTaskItems));
	}
}
