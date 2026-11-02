using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Api.Extensions;
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Shared;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Tasks;

public class CreateTaskItem : IEndpoint
{
	public record CreateTaskItemRequest(string Title, string Description);
	public record CreateTaskItemResponse(Guid Id, string Title, string Description, TaskItemStatus Status, DateTimeOffset CreatedAt);

	public async Task<IResult> Handler(AppDbContext context, CreateTaskItemRequest request, CancellationToken cancellationToken)
	{
		var result = await TaskItem.Create(request.Title, request.Description)
			.Tap(t => context.Tasks.Add(t))
			.TapAsync(async _ => await context.SaveChangesAsync(cancellationToken));

		return result
			.Map(t => new CreateTaskItemResponse(t.Id, t.Title, t.Description, t.Status, t.CreatedAt))
			.ToCreatedAtRouteOrProblem(nameof(GetTaskItemById), r => new { id = r.Id });
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPost("/tasks", Handler)
			.WithTags("Tasks")
			.WithName(nameof(CreateTaskItem));
	}
}
