using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
#if (cqrs)
using CleanSlices.Core.Domain;
#endif
using CleanSlices.Core.Domain.Widgets;
#if (cqrs && result)
using CleanSlices.Core.Shared;
#endif
#if (!repository && !cqrs)
using CleanSlices.Infrastructure.Persistence;
#endif
#if (!repository || cqrs)
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Widgets;

public class GetWidgetById : IEndpoint
{
	public record GetWidgetByIdResponse(Guid Id, string Name, DateTimeOffset CreatedAt);

#if (cqrs && result)
	public sealed record GetWidgetByIdQuery(Guid Id) : IQuery<Result<GetWidgetByIdResponse>>;

	internal sealed class GetWidgetByIdHandler(IReadDb db) : IQueryHandler<GetWidgetByIdQuery, Result<GetWidgetByIdResponse>>
	{
		public async ValueTask<Result<GetWidgetByIdResponse>> Handle(GetWidgetByIdQuery query, CancellationToken cancellationToken)
		{
			var entity = await db.Query<Widget>()
				.Where(e => e.Id == query.Id)
				.Select(e => new GetWidgetByIdResponse(e.Id, e.Name, e.CreatedAt))
				.FirstOrDefaultAsync(cancellationToken);

			return entity is null
				? Result.Failure<GetWidgetByIdResponse>(WidgetErrors.NotFound(query.Id))
				: entity;
		}
	}
#elif (cqrs)
	public sealed record GetWidgetByIdQuery(Guid Id) : IQuery<GetWidgetByIdResponse>;

	internal sealed class GetWidgetByIdHandler(IReadDb db) : IQueryHandler<GetWidgetByIdQuery, GetWidgetByIdResponse>
	{
		public async ValueTask<GetWidgetByIdResponse> Handle(GetWidgetByIdQuery query, CancellationToken cancellationToken)
		{
			var entity = await db.Query<Widget>()
				.Where(e => e.Id == query.Id)
				.Select(e => new GetWidgetByIdResponse(e.Id, e.Name, e.CreatedAt))
				.FirstOrDefaultAsync(cancellationToken);

			return entity ?? throw WidgetErrors.NotFound(query.Id);
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new GetWidgetByIdQuery(id), cancellationToken);
		return result.ToOkOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken) =>
		Results.Ok(await sender.Send(new GetWidgetByIdQuery(id), cancellationToken));
#elif (repository && result)
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
