using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Infrastructure.Persistence;

namespace CleanSlices.Api.Features.Tasks;

public class CreateTaskItem : IEndpoint
{
	public record CreateTaskItemRequest(string Title, string Description);
	public record CreateTaskItemResponse(Guid Id, string Title, string Description, TaskItemStatus Status, DateTimeOffset CreatedAt);

	public async Task<IResult> Handler(AppDbContext context, CreateTaskItemRequest request, CancellationToken cancellationToken)
	{
		var task = TaskItem.Create(request.Title, request.Description);
		context.Tasks.Add(task);
		await context.SaveChangesAsync(cancellationToken);

		var response = new CreateTaskItemResponse(task.Id, task.Title, task.Description, task.Status, task.CreatedAt);
		return Results.CreatedAtRoute(nameof(GetTaskItemById), new { id = response.Id }, response);
	}

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapPost("/tasks", Handler)
			.WithTags("Tasks")
			.WithName(nameof(CreateTaskItem));
	}
}
