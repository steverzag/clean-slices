using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Core.Domain.Widgets;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Widgets;

public class CreateWidget : IEndpoint
{
	public record CreateWidgetRequest(string Name);
	public record CreateWidgetResponse(Guid Id, string Name);

	public async Task<IResult> Handler(AppDbContext context, CreateWidgetRequest request, CancellationToken cancellationToken)
	{
		var entity = Widget.Create(request.Name);
		context.Set<Widget>().Add(entity);
		await context.SaveChangesAsync(cancellationToken);

		var response = new CreateWidgetResponse(entity.Id, entity.Name);
		return Results.CreatedAtRoute(nameof(GetWidgetById), new { id = response.Id }, response);
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPost("/widgets", Handler)
			.WithTags("Widgets")
			.WithName(nameof(CreateWidget));
	}
}
