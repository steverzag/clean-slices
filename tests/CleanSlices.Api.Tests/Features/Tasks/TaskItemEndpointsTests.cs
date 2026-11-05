using System.Net;
using CleanSlices.Api.Features.Tasks;
using CleanSlices.Api.Features.Users;
using CleanSlices.Api.Tests.Infrastructure;
using CleanSlices.Core.Domain.Tasks;

namespace CleanSlices.Api.Tests.Features.Tasks;

public class TaskItemEndpointsTests(ApiFactory factory)
{
	private readonly HttpClient _client = factory.CreateClient();

	[Fact]
	public async Task Create_returns_201_and_the_task_can_be_read()
	{
		var response = await _client.Post("/tasks", new CreateTaskItem.CreateTaskItemRequest("Write tests", "Cover the API"));

		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		var created = await response.Content.ReadAsAsync<CreateTaskItem.CreateTaskItemResponse>();

		var task = await GetTaskAsync(created.Id);
		Assert.Equal("Write tests", task.Title);
		Assert.Equal(TaskItemStatus.NotStarted, task.Status);
		Assert.Empty(task.UserIds);
	}

	[Fact]
	public async Task Create_with_invalid_fields_returns_400_with_errors_per_field()
	{
		var response = await _client.Post("/tasks", new CreateTaskItem.CreateTaskItemRequest("", ""));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		var problem = await response.Content.ReadValidationProblemAsync();
		Assert.Contains("title", problem.Errors.Keys);
		Assert.Contains("description", problem.Errors.Keys);
	}

	[Fact]
	public async Task Completed_sets_the_status()
	{
		var id = await CreateTaskAsync();

		var response = await _client.Patch($"/tasks/{id}/completed");

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		Assert.Equal(TaskItemStatus.Completed, (await GetTaskAsync(id)).Status);
	}

	[Fact]
	public async Task AssignUser_adds_the_user_to_the_task()
	{
		var taskId = await CreateTaskAsync();
		var userId = await CreateUserAsync();

		var response = await _client.Post($"/tasks/{taskId}/users/{userId}");

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		Assert.Equal(userId, Assert.Single((await GetTaskAsync(taskId)).UserIds));
	}

	[Fact]
	public async Task AssignUser_with_an_unknown_user_returns_404()
	{
		var taskId = await CreateTaskAsync();

		var response = await _client.Post($"/tasks/{taskId}/users/{Guid.CreateVersion7()}");

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task Deleting_a_user_unassigns_them_from_their_tasks()
	{
		var taskId = await CreateTaskAsync();
		var userId = await CreateUserAsync();
		await _client.Post($"/tasks/{taskId}/users/{userId}");

		var response = await _client.Delete($"/users/{userId}");

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		Assert.Empty((await GetTaskAsync(taskId)).UserIds);
	}

	[Fact]
	public async Task Delete_removes_the_task()
	{
		var id = await CreateTaskAsync();

		var response = await _client.Delete($"/tasks/{id}");

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await _client.Get($"/tasks/{id}")).StatusCode);
	}

	private async Task<Guid> CreateTaskAsync()
	{
		var response = await _client.Post("/tasks", new CreateTaskItem.CreateTaskItemRequest("Write tests", "Cover the API"));
		return (await response.Content.ReadAsAsync<CreateTaskItem.CreateTaskItemResponse>()).Id;
	}

	private async Task<Guid> CreateUserAsync()
	{
		var response = await _client.Post("/users", new CreateUser.CreateUserRequest("Ana", $"{Guid.NewGuid():N}@example.com"));
		return (await response.Content.ReadAsAsync<CreateUser.CreateUserResponse>()).Id;
	}

	private async Task<GetTaskItemById.TaskItemResponse> GetTaskAsync(Guid id) =>
		await (await _client.Get($"/tasks/{id}")).Content.ReadAsAsync<GetTaskItemById.TaskItemResponse>();
}
