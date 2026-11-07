#if (!repository || cqrs)
using CleanSlices.Api.Configuration;
#endif
using CleanSlices.Api.Configuration.Endpoints;
#if (cqrs)
using CleanSlices.Core.Domain;
#endif
using CleanSlices.Core.Domain.Widgets;
using CleanSlices.Infrastructure.Pagination;
#if (!repository && !cqrs)
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Widgets;

public class GetWidgets : IEndpoint
{
	public record WidgetListItemResponse(Guid Id, string Name, DateTimeOffset CreatedAt);

#if (cqrs)
	public sealed record GetWidgetsQuery(int PageNumber, int PageSize) : IQuery<PaginationResponse<WidgetListItemResponse>>;

	internal sealed class GetWidgetsHandler(IReadDb db) : IQueryHandler<GetWidgetsQuery, PaginationResponse<WidgetListItemResponse>>
	{
		public async ValueTask<PaginationResponse<WidgetListItemResponse>> Handle(GetWidgetsQuery query, CancellationToken cancellationToken)
		{
			var items = await db.Query<Widget>()
				.OrderByDescending(e => e.CreatedAt)
				.ThenByDescending(e => e.Id)
				.Select(e => new WidgetListItemResponse(e.Id, e.Name, e.CreatedAt))
				.PaginateAsync(query.PageNumber, query.PageSize, cancellationToken);

			return items.ToResponse();
		}
	}

	public async Task<IResult> Handler(ISender sender, [AsParameters] PaginationParameters paging, CancellationToken cancellationToken) =>
		Results.Ok(await sender.Send(new GetWidgetsQuery(paging.PageNumber, paging.PageSize), cancellationToken));
#elif (repository)
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
