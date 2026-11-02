using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Core.Domain.Widgets;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Widgets;

public class DeleteWidget : IEndpoint
{
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var entity = await context.Set<Widget>().FindAsync([id], cancellationToken);
		if (entity is null)
		{
			throw WidgetErrors.NotFound(id);
		}

		entity.Delete();
		context.Set<Widget>().Remove(entity);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapDelete("/widgets/{id:guid}", Handler)
			.WithTags("Widgets")
			.WithName(nameof(DeleteWidget));
	}
}
