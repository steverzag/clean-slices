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

public class UpdateWidgetName : IEndpoint
{
	public record UpdateWidgetNameRequest(string Name);
#if (validation)

	public sealed class UpdateWidgetNameRequestValidator : AbstractValidator<UpdateWidgetNameRequest>
	{
		public UpdateWidgetNameRequestValidator()
		{
			RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
				.NotEmpty().WithMessage(WidgetFieldErrors.EmptyName.Description)
				.MaximumLength(Widget.NameMaxLength).WithMessage(WidgetFieldErrors.NameTooLong.Description);
		}
	}
#endif

#if (result)
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
#else
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
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPatch("/widgets/{id:guid}/name", Handler)
			.WithTags("Widgets")
			.WithName(nameof(UpdateWidgetName));
	}
}
