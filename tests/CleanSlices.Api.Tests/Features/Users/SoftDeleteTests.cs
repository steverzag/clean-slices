#if (soft_delete)
using System.Net;
using CleanSlices.Api.Features.Tasks;
using CleanSlices.Api.Features.Users;
using CleanSlices.Api.Tests.Infrastructure;
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Domain.Users;
using CleanSlices.Infrastructure.Persistence;
using CleanSlices.Infrastructure.Persistence.SoftDelete;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Api.Tests.Features.Users;

// The API behaves as after a real delete (the endpoint tests check that); these check what stays in the database.
public class SoftDeleteTests(ApiFactory factory)
{
	private readonly HttpClient _client = factory.CreateClient();

	[Fact]
	public async Task A_deleted_user_keeps_its_row_with_the_time_it_was_deleted()
	{
		var response = await _client.Post("/users", new CreateUser.CreateUserRequest("Ana", $"{Guid.NewGuid():N}@example.com"));
		var id = (await response.Content.ReadAsAsync<CreateUser.CreateUserResponse>()).Id;

		await _client.Delete($"/users/{id}");

		await using var scope = factory.Services.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		Assert.False(await context.Set<User>().AnyAsync(u => u.Id == id, TestContext.Current.CancellationToken));
		var row = await context.Set<User>()
			.IgnoreQueryFilters([SoftDeleteFilter.Name])
			.SingleAsync(u => u.Id == id, TestContext.Current.CancellationToken);
		Assert.NotNull(row.DeletedAt);
	}

	// The delete only marks the task: the database's cascade never runs, so a restored task gets its users back.
	[Fact]
	public async Task A_deleted_task_keeps_its_row_and_its_assignments()
	{
		var task = await _client.Post("/tasks", new CreateTaskItem.CreateTaskItemRequest("Soft", "Deleted later"));
		var taskId = (await task.Content.ReadAsAsync<CreateTaskItem.CreateTaskItemResponse>()).Id;
		var user = await _client.Post("/users", new CreateUser.CreateUserRequest("Ana", $"{Guid.NewGuid():N}@example.com"));
		var userId = (await user.Content.ReadAsAsync<CreateUser.CreateUserResponse>()).Id;
		await _client.Post($"/tasks/{taskId}/users/{userId}");

		var response = await _client.Delete($"/tasks/{taskId}");

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		await using var scope = factory.Services.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		Assert.True(await context.Set<TaskItem>().IgnoreQueryFilters([SoftDeleteFilter.Name])
			.AnyAsync(t => t.Id == taskId && t.DeletedAt != null, TestContext.Current.CancellationToken));
		Assert.True(await context.Set<TaskItemUserAssignment>()
			.AnyAsync(a => a.TaskItemId == taskId, TestContext.Current.CancellationToken));
	}
}
#endif
