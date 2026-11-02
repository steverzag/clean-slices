using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Domain.Widgets;
using CleanSlices.Core.Shared;
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
			return WidgetErrors.NotFound(id).ToProblem();
		}

		var result = await entity.UpdateName(request.Name)
			.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/widgets/{id:guid}/name", Handler)
			.WithTags("Widgets")
			.WithName(nameof(UpdateWidgetName));
	}
}
