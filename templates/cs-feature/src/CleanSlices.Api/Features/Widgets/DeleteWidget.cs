using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
using CleanSlices.Core.Domain.Widgets;
#if (repository)
using CleanSlices.Core.Domain;
#endif
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
#endif

namespace CleanSlices.Api.Features.Widgets;

public class DeleteWidget : IEndpoint
{
#if (repository && result)
	public async Task<IResult> Handler(IWidgetRepository widgets, IUnitOfWork unitOfWork, Guid id, CancellationToken cancellationToken)
	{
		var entity = await widgets.GetByIdAsync(id, cancellationToken);
		if (entity is null)
		{
			return WidgetErrors.NotFound(id).ToProblem();
		}

		entity.Delete();
		widgets.Remove(entity);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#elif (repository)
	public async Task<IResult> Handler(IWidgetRepository widgets, IUnitOfWork unitOfWork, Guid id, CancellationToken cancellationToken)
	{
		var entity = await widgets.GetByIdAsync(id, cancellationToken);
		if (entity is null)
		{
			throw WidgetErrors.NotFound(id);
		}

		entity.Delete();
		widgets.Remove(entity);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#elif (result)
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var entity = await context.Set<Widget>().FindAsync([id], cancellationToken);
		if (entity is null)
		{
			return WidgetErrors.NotFound(id).ToProblem();
		}

		entity.Delete();
		context.Set<Widget>().Remove(entity);
		await context.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#else
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
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapDelete("/widgets/{id:guid}", Handler)
			.WithTags("Widgets")
			.WithName(nameof(DeleteWidget));
	}
}
