#if (!repository)
using CleanSlices.Api.Configuration;
#endif
using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Core.Domain.Widgets;
using CleanSlices.Infrastructure.Pagination;
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Widgets;

public class GetWidgets : IEndpoint
{
	public record WidgetListItemResponse(Guid Id, string Name, DateTimeOffset CreatedAt);

#if (repository)
	public async Task<IResult> Handler(IWidgetRepository widgets, [AsParameters] PaginationParameters paging, CancellationToken cancellationToken)
	{
		var page = await widgets.GetPageAsync(paging.PageNumber, paging.PageSize, cancellationToken);

		return Results.Ok(page.Map(e => new WidgetListItemResponse(e.Id, e.Name, e.CreatedAt)));
	}
#else
	public async Task<IResult> Handler(AppDbContext context, [AsParameters] PaginationParameters paging, CancellationToken cancellationToken)
	{
		var items = await context.Set<Widget>()
			.AsNoTracking()
			.OrderByDescending(e => e.CreatedAt)
			.ThenByDescending(e => e.Id)
			.Select(e => new WidgetListItemResponse(e.Id, e.Name, e.CreatedAt))
			.PaginateAsync(paging, cancellationToken);

		return Results.Ok(items.ToResponse());
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/widgets", Handler)
			.WithTags("Widgets")
			.WithName(nameof(GetWidgets));
	}
}
