using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Core.Domain.Widgets;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Widgets;

public class GetWidgets : IEndpoint
{
	public record WidgetListItemResponse(Guid Id, string Name, DateTimeOffset CreatedAt);

	public async Task<IResult> Handler(AppDbContext context, CancellationToken cancellationToken)
	{
		var items = await context.Set<Widget>()
			.AsNoTracking()
			.Select(e => new WidgetListItemResponse(e.Id, e.Name, e.CreatedAt))
			.ToListAsync(cancellationToken);

		return Results.Ok(items);
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/widgets", Handler)
			.WithTags("Widgets")
			.WithName(nameof(GetWidgets));
	}
}
