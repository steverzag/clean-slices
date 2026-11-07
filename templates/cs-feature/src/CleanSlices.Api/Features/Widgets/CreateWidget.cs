using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
using CleanSlices.Core.Domain.Widgets;
#if (result)
using CleanSlices.Core.Shared;
#endif
using CleanSlices.Infrastructure.Persistence;
#if (validation)
using FluentValidation;
using WidgetFieldErrors = CleanSlices.Core.Domain.Widgets.WidgetErrors;
#endif

namespace CleanSlices.Api.Features.Widgets;

public class CreateWidget : IEndpoint
{
	public record CreateWidgetRequest(string Name);
#if (validation)

	public sealed class CreateWidgetRequestValidator : AbstractValidator<CreateWidgetRequest>
	{
		public CreateWidgetRequestValidator()
		{
			RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
				.NotEmpty().WithMessage(WidgetFieldErrors.EmptyName.Description)
				.MaximumLength(Widget.NameMaxLength).WithMessage(WidgetFieldErrors.NameTooLong.Description);
		}
	}
#endif
	public record CreateWidgetResponse(Guid Id, string Name);

	public async Task<IResult> Handler(AppDbContext context, CreateWidgetRequest request, CancellationToken cancellationToken)
	{
#if (result)
		var result = await Widget.Create(request.Name)
			.Tap(e => context.Set<Widget>().Add(e))
			.TapAsync(async _ => await context.SaveChangesAsync(cancellationToken));

		return result
			.Map(e => new CreateWidgetResponse(e.Id, e.Name))
			.ToCreatedAtRouteOrProblem(nameof(GetWidgetById), r => new { id = r.Id });
#else
		var entity = Widget.Create(request.Name);
		context.Set<Widget>().Add(entity);
		await context.SaveChangesAsync(cancellationToken);

		var response = new CreateWidgetResponse(entity.Id, entity.Name);
		return Results.CreatedAtRoute(nameof(GetWidgetById), new { id = response.Id }, response);
#endif
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPost("/widgets", Handler)
			.WithTags("Widgets")
			.WithName(nameof(CreateWidget));
	}
}
