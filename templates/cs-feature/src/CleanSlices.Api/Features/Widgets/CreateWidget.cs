using CleanSlices.Api.Configuration.Endpoints;
#if (idempotency)
using CleanSlices.Api.Configuration.Idempotency;
#endif
#if (result)
using CleanSlices.Api.Extensions;
#endif
using CleanSlices.Core.Domain.Widgets;
#if (result)
using CleanSlices.Core.Shared;
#endif
#if (repository)
using CleanSlices.Core.Domain;
#endif
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
#endif
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

#if (repository && result)
	public async Task<IResult> Handler(IWidgetRepository widgets, IUnitOfWork unitOfWork, CreateWidgetRequest request, CancellationToken cancellationToken)
	{
		var result = await Widget.Create(request.Name)
			.Tap(e => widgets.Add(e))
			.TapAsync(async _ => await unitOfWork.SaveChangesAsync(cancellationToken));

		return result
			.Map(e => new CreateWidgetResponse(e.Id, e.Name))
			.ToCreatedAtRouteOrProblem(nameof(GetWidgetById), r => new { id = r.Id });
	}
#elif (repository)
	public async Task<IResult> Handler(IWidgetRepository widgets, IUnitOfWork unitOfWork, CreateWidgetRequest request, CancellationToken cancellationToken)
	{
		var entity = Widget.Create(request.Name);
		widgets.Add(entity);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		var response = new CreateWidgetResponse(entity.Id, entity.Name);
		return Results.CreatedAtRoute(nameof(GetWidgetById), new { id = response.Id }, response);
	}
#elif (result)
	public async Task<IResult> Handler(AppDbContext context, CreateWidgetRequest request, CancellationToken cancellationToken)
	{
		var result = await Widget.Create(request.Name)
			.Tap(e => context.Set<Widget>().Add(e))
			.TapAsync(async _ => await context.SaveChangesAsync(cancellationToken));

		return result
			.Map(e => new CreateWidgetResponse(e.Id, e.Name))
			.ToCreatedAtRouteOrProblem(nameof(GetWidgetById), r => new { id = r.Id });
	}
#else
	public async Task<IResult> Handler(AppDbContext context, CreateWidgetRequest request, CancellationToken cancellationToken)
	{
		var entity = Widget.Create(request.Name);
		context.Set<Widget>().Add(entity);
		await context.SaveChangesAsync(cancellationToken);

		var response = new CreateWidgetResponse(entity.Id, entity.Name);
		return Results.CreatedAtRoute(nameof(GetWidgetById), new { id = response.Id }, response);
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPost("/widgets", Handler)
			.WithTags("Widgets")
#if (idempotency)
			.WithIdempotency()
#endif
			.WithName(nameof(CreateWidget));
	}
}
