using CleanSlices.Api.Configuration;
using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Core.Domain.Widgets;
using CleanSlices.Infrastructure.Pagination;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Widgets;

public class GetWidgets : IEndpoint
{
	public record WidgetListItemResponse(Guid Id, string Name, DateTimeOffset CreatedAt);

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

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/widgets", Handler)
			.WithTags("Widgets")
			.WithName(nameof(GetWidgets));
	}
}
