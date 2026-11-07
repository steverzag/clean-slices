using CleanSlices.Api.Configuration.Endpoints;
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

#if (cqrs && result)
	public sealed record UpdateWidgetNameCommand(Guid Id, string Name) : ICommand<Result>;

#elif (cqrs)
	public sealed record UpdateWidgetNameCommand(Guid Id, string Name) : ICommand;

#endif
#if (cqrs && repository && result)
	internal sealed class UpdateWidgetNameHandler(IWidgetRepository widgets, IUnitOfWork unitOfWork) : ICommandHandler<UpdateWidgetNameCommand, Result>
	{
		public async ValueTask<Result> Handle(UpdateWidgetNameCommand command, CancellationToken cancellationToken)
		{
			var entity = await widgets.GetByIdAsync(command.Id, cancellationToken);
			if (entity is null)
			{
				return Result.Failure(WidgetErrors.NotFound(command.Id));
			}

			var result = await entity.UpdateName(command.Name)
				.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs && repository)
	internal sealed class UpdateWidgetNameHandler(IWidgetRepository widgets, IUnitOfWork unitOfWork) : ICommandHandler<UpdateWidgetNameCommand>
	{
		public async ValueTask<Unit> Handle(UpdateWidgetNameCommand command, CancellationToken cancellationToken)
		{
			var entity = await widgets.GetByIdAsync(command.Id, cancellationToken);
			if (entity is null)
			{
				throw WidgetErrors.NotFound(command.Id);
			}

			entity.UpdateName(command.Name);
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#elif (cqrs && result)
	internal sealed class UpdateWidgetNameHandler(AppDbContext context) : ICommandHandler<UpdateWidgetNameCommand, Result>
	{
		public async ValueTask<Result> Handle(UpdateWidgetNameCommand command, CancellationToken cancellationToken)
		{
			var entity = await context.Set<Widget>().FindAsync([command.Id], cancellationToken);
			if (entity is null)
			{
				return Result.Failure(WidgetErrors.NotFound(command.Id));
			}

			var result = await entity.UpdateName(command.Name)
				.TapAsync(async () => await context.SaveChangesAsync(cancellationToken));

			return result;
		}
	}
#elif (cqrs)
	internal sealed class UpdateWidgetNameHandler(AppDbContext context) : ICommandHandler<UpdateWidgetNameCommand>
	{
		public async ValueTask<Unit> Handle(UpdateWidgetNameCommand command, CancellationToken cancellationToken)
		{
			var entity = await context.Set<Widget>().FindAsync([command.Id], cancellationToken);
			if (entity is null)
			{
				throw WidgetErrors.NotFound(command.Id);
			}

			entity.UpdateName(command.Name);
			await context.SaveChangesAsync(cancellationToken);

			return Unit.Value;
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, UpdateWidgetNameRequest request, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new UpdateWidgetNameCommand(id, request.Name), cancellationToken);
		return result.ToNoContentOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, UpdateWidgetNameRequest request, CancellationToken cancellationToken)
	{
		await sender.Send(new UpdateWidgetNameCommand(id, request.Name), cancellationToken);
		return Results.NoContent();
	}
#elif (repository && result)
	public async Task<IResult> Handler(IWidgetRepository widgets, IUnitOfWork unitOfWork, Guid id, UpdateWidgetNameRequest request, CancellationToken cancellationToken)
	{
		var entity = await widgets.GetByIdAsync(id, cancellationToken);
		if (entity is null)
		{
			return WidgetErrors.NotFound(id).ToProblem();
		}

		var result = await entity.UpdateName(request.Name)
			.TapAsync(async () => await unitOfWork.SaveChangesAsync(cancellationToken));

		return result.ToNoContentOrProblem();
	}
#elif (repository)
	public async Task<IResult> Handler(IWidgetRepository widgets, IUnitOfWork unitOfWork, Guid id, UpdateWidgetNameRequest request, CancellationToken cancellationToken)
	{
		var entity = await widgets.GetByIdAsync(id, cancellationToken);
		if (entity is null)
		{
			throw WidgetErrors.NotFound(id);
		}

		entity.UpdateName(request.Name);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return Results.NoContent();
	}
#elif (result)
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
