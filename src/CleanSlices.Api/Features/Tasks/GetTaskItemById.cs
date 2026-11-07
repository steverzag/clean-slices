using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
#if (cqrs)
using CleanSlices.Core.Domain;
#endif
using CleanSlices.Core.Domain.Tasks;
#if (cqrs && result)
using CleanSlices.Core.Shared;
#endif
#if (!repository && !cqrs)
using CleanSlices.Infrastructure.Persistence;
#endif
#if (!repository || cqrs)
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Tasks;

public class GetTaskItemById : IEndpoint
{
	public record TaskItemResponse(
		Guid Id,
		string Title,
		string Description,
		TaskItemStatus Status,
		DateTimeOffset CreatedAt,
		IReadOnlyList<Guid> UserIds);

#if (cqrs && result)
	public sealed record GetTaskItemByIdQuery(Guid Id) : IQuery<Result<TaskItemResponse>>;

	internal sealed class GetTaskItemByIdHandler(IReadDb db) : IQueryHandler<GetTaskItemByIdQuery, Result<TaskItemResponse>>
	{
		public async ValueTask<Result<TaskItemResponse>> Handle(GetTaskItemByIdQuery query, CancellationToken cancellationToken)
		{
			var task = await db.Query<TaskItem>()
				.Where(t => t.Id == query.Id)
				.Select(t => new TaskItemResponse(
					t.Id,
					t.Title,
					t.Description,
					t.Status,
					t.CreatedAt,
					t.UserAssignments
						.OrderBy(a => a.AssignedOn)
						.Select(a => a.UserId)
						.ToList()))
				.FirstOrDefaultAsync(cancellationToken);

			return task is null
				? Result.Failure<TaskItemResponse>(TaskItemErrors.NotFound(query.Id))
				: task;
		}
	}
#elif (cqrs)
	public sealed record GetTaskItemByIdQuery(Guid Id) : IQuery<TaskItemResponse>;

	internal sealed class GetTaskItemByIdHandler(IReadDb db) : IQueryHandler<GetTaskItemByIdQuery, TaskItemResponse>
	{
		public async ValueTask<TaskItemResponse> Handle(GetTaskItemByIdQuery query, CancellationToken cancellationToken)
		{
			var task = await db.Query<TaskItem>()
				.Where(t => t.Id == query.Id)
				.Select(t => new TaskItemResponse(
					t.Id,
					t.Title,
					t.Description,
					t.Status,
					t.CreatedAt,
					t.UserAssignments
						.OrderBy(a => a.AssignedOn)
						.Select(a => a.UserId)
						.ToList()))
				.FirstOrDefaultAsync(cancellationToken);

			return task ?? throw TaskItemErrors.NotFound(query.Id);
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new GetTaskItemByIdQuery(id), cancellationToken);
		return result.ToOkOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken) =>
		Results.Ok(await sender.Send(new GetTaskItemByIdQuery(id), cancellationToken));
#elif (repository && result)
	public async Task<IResult> Handler(ITaskItemRepository tasks, Guid id, CancellationToken cancellationToken)
	{
		var task = await tasks.GetWithAssignmentsAsync(id, cancellationToken);

		return task is null
			? TaskItemErrors.NotFound(id).ToProblem()
			: Results.Ok(new TaskItemResponse(
				task.Id,
				task.Title,
				task.Description,
				task.Status,
				task.CreatedAt,
				[.. task.UserAssignments.OrderBy(a => a.AssignedOn).Select(a => a.UserId)]));
	}
#elif (repository)
	public async Task<IResult> Handler(ITaskItemRepository tasks, Guid id, CancellationToken cancellationToken)
	{
		var task = await tasks.GetWithAssignmentsAsync(id, cancellationToken);

		return task is null
			? throw TaskItemErrors.NotFound(id)
			: Results.Ok(new TaskItemResponse(
				task.Id,
				task.Title,
				task.Description,
				task.Status,
				task.CreatedAt,
				[.. task.UserAssignments.OrderBy(a => a.AssignedOn).Select(a => a.UserId)]));
	}
#elif (result)
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var task = await context.Tasks
			.AsNoTracking()
			.Where(t => t.Id == id)
			.Select(t => new TaskItemResponse(
				t.Id,
				t.Title,
				t.Description,
				t.Status,
				t.CreatedAt,
				t.UserAssignments
					.OrderBy(a => a.AssignedOn)
					.Select(a => a.UserId)
					.ToList()))
			.FirstOrDefaultAsync(cancellationToken);

		return task is null
			? TaskItemErrors.NotFound(id).ToProblem()
			: Results.Ok(task);
	}
#else
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var task = await context.Tasks
			.AsNoTracking()
			.Where(t => t.Id == id)
			.Select(t => new TaskItemResponse(
				t.Id,
				t.Title,
				t.Description,
				t.Status,
				t.CreatedAt,
				t.UserAssignments
					.OrderBy(a => a.AssignedOn)
					.Select(a => a.UserId)
					.ToList()))
			.FirstOrDefaultAsync(cancellationToken);

		return task is null
			? throw TaskItemErrors.NotFound(id)
			: Results.Ok(task);
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/tasks/{id:guid}", Handler)
			.WithTags("Tasks")
			.WithName(nameof(GetTaskItemById));
	}
}
