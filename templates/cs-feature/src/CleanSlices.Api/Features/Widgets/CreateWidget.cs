using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Domain.Widgets;
using CleanSlices.Core.Shared;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Widgets;

public class CreateWidget : IEndpoint
{
	public record CreateWidgetRequest(string Name);
	public record CreateWidgetResponse(Guid Id, string Name);

	public async Task<IResult> Handler(AppDbContext context, CreateWidgetRequest request, CancellationToken cancellationToken)
	{
		var result = await Widget.Create(request.Name)
			.Tap(e => context.Set<Widget>().Add(e))
			.TapAsync(async _ => await context.SaveChangesAsync(cancellationToken));

		return result
			.Map(e => new CreateWidgetResponse(e.Id, e.Name))
			.ToCreatedAtRouteOrProblem(nameof(GetWidgetById), r => new { id = r.Id });
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPost("/widgets", Handler)
			.WithTags("Widgets")
			.WithName(nameof(CreateWidget));
	}
}
