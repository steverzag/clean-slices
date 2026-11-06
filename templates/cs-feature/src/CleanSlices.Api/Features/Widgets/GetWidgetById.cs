using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
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
#if (result)
			? WidgetErrors.NotFound(id).ToProblem()
#else
			? throw WidgetErrors.NotFound(id)
#endif
			: Results.Ok(entity);
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/widgets/{id:guid}", Handler)
			.WithTags("Widgets")
			.WithName(nameof(GetWidgetById));
	}
}
