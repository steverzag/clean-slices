using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Core.Domain.Widgets;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Widgets;

public class UpdateWidgetName : IEndpoint
{
	public record UpdateWidgetNameRequest(string Name);

	public async Task<IResult> Handler(AppDbContext context, Guid id, UpdateWidgetNameRequest request, CancellationToken cancellationToken)
	{
		var entity = await context.Set<Widget>().FindAsync([id], cancellationToken);
		if (entity is null)
		{
			throw WidgetErrors.NotFound(id);
		}

		entity.UpdateName(request.Name);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/widgets/{id:guid}/name", Handler)
			.WithTags("Widgets")
			.WithName(nameof(UpdateWidgetName));
	}
}
