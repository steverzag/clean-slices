using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Core.Domain.Widgets;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Api.Features.Widgets;

public class GetWidgetById : IEndpoint
{
	public record GetWidgetByIdResponse(Guid Id, string Name, DateTimeOffset CreatedAt);

	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var entity = await context.Set<Widget>()
			.AsNoTracking()
			.Where(e => e.Id == id)
			.Select(e => new GetWidgetByIdResponse(e.Id, e.Name, e.CreatedAt))
			.FirstOrDefaultAsync(cancellationToken);

		return entity is null
			? throw WidgetErrors.NotFound(id)
			: Results.Ok(entity);
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/widgets/{id:guid}", Handler)
			.WithTags("Widgets")
			.WithName(nameof(GetWidgetById));
	}
}
