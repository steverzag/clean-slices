using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
using CleanSlices.Core.Domain.Widgets;
#if (repository)
using CleanSlices.Core.Domain;
#endif
#if (cqrs && result)
using CleanSlices.Core.Shared;
#endif
#if (!repository)
using CleanSlices.Infrastructure.Persistence;
#endif

namespace CleanSlices.Api.Features.Widgets;

public class DeleteWidget : IEndpoint
{
#if (cqrs && result)
	public sealed record DeleteWidgetCommand(Guid Id) : ICommand<Result>;

#elif (cqrs)
	public sealed record DeleteWidgetCommand(Guid Id) : ICommand;

#endif
#if (cqrs && repository && result)
	internal sealed class DeleteWidgetHandler(IWidgetRepository widgets, IUnitOfWork unitOfWork) : ICommandHandler<DeleteWidgetCommand, Result>
	{
		public async ValueTask<Result> Handle(DeleteWidgetCommand command, CancellationToken cancellationToken)
		{
			var entity = await widgets.GetByIdAsync(command.Id, cancellationToken);
			if (entity is null)
			{
				return Result.Failure(WidgetErrors.NotFound(command.Id));
			}

			entity.Delete();
			widgets.Remove(entity);
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Result.Success();
		}
	}
#elif (cqrs && repository)
	internal sealed class DeleteWidgetHandler(IWidgetRepository widgets, IUnitOfWork unitOfWork) : ICommandHandler<DeleteWidgetCommand>
	{
		public async ValueTask<Unit> Handle(DeleteWidgetCommand command, CancellationToken cancellationToken)
		{
			var entity = await widgets.GetByIdAsync(command.Id, cancellationToken);
			if (entity is null)
			{
				throw WidgetErrors.NotFound(command.Id);
			}

			entity.Delete();
			widgets.Remove(entity);
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#elif (cqrs && result)
	internal sealed class DeleteWidgetHandler(AppDbContext context) : ICommandHandler<DeleteWidgetCommand, Result>
	{
		public async ValueTask<Result> Handle(DeleteWidgetCommand command, CancellationToken cancellationToken)
		{
			var entity = await context.Set<Widget>().FindAsync([command.Id], cancellationToken);
			if (entity is null)
			{
				return Result.Failure(WidgetErrors.NotFound(command.Id));
			}

			entity.Delete();
			context.Set<Widget>().Remove(entity);
			await context.SaveChangesAsync(cancellationToken);

			return Result.Success();
		}
	}
#elif (cqrs)
	internal sealed class DeleteWidgetHandler(AppDbContext context) : ICommandHandler<DeleteWidgetCommand>
	{
		public async ValueTask<Unit> Handle(DeleteWidgetCommand command, CancellationToken cancellationToken)
		{
			var entity = await context.Set<Widget>().FindAsync([command.Id], cancellationToken);
			if (entity is null)
			{
				throw WidgetErrors.NotFound(command.Id);
			}

			entity.Delete();
			context.Set<Widget>().Remove(entity);
			await context.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new DeleteWidgetCommand(id), cancellationToken);
		return result.ToNoContentOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken)
	{
		await sender.Send(new DeleteWidgetCommand(id), cancellationToken);
		return Results.NoContent();
	}
#elif (repository && result)
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
