using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
using CleanSlices.Core.Domain.Widgets;
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Widgets;

public class GetWidgetById : IEndpoint
{
	public record GetWidgetByIdResponse(Guid Id, string Name, DateTimeOffset CreatedAt);

#if (repository && result)
	public async Task<IResult> Handler(IWidgetRepository widgets, Guid id, CancellationToken cancellationToken)
	{
		var entity = await widgets.GetByIdAsync(id, cancellationToken);

		return entity is null
			? WidgetErrors.NotFound(id).ToProblem()
			: Results.Ok(new GetWidgetByIdResponse(entity.Id, entity.Name, entity.CreatedAt));
	}
#elif (repository)
	public async Task<IResult> Handler(IWidgetRepository widgets, Guid id, CancellationToken cancellationToken)
	{
		var entity = await widgets.GetByIdAsync(id, cancellationToken);

		return entity is null
			? throw WidgetErrors.NotFound(id)
			: Results.Ok(new GetWidgetByIdResponse(entity.Id, entity.Name, entity.CreatedAt));
	}
#elif (result)
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var entity = await context.Set<Widget>()
			.AsNoTracking()
			.Where(e => e.Id == id)
			.Select(e => new GetWidgetByIdResponse(e.Id, e.Name, e.CreatedAt))
			.FirstOrDefaultAsync(cancellationToken);

		return entity is null
			? WidgetErrors.NotFound(id).ToProblem()
			: Results.Ok(entity);
	}
#else
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
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/widgets/{id:guid}", Handler)
			.WithTags("Widgets")
			.WithName(nameof(GetWidgetById));
	}
}
