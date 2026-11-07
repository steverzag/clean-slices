#if (outbox)
using CleanSlices.Api.Features.Tasks;
using CleanSlices.Api.Features.Users;
using CleanSlices.Api.Tests.Infrastructure;
using CleanSlices.Api.Tests.Outbox;
using CleanSlices.Infrastructure.Outbox;

namespace CleanSlices.Api.Tests.Features.Tasks;

public class TaskIntegrationEventsTests(ApiFactory factory)
{
	private readonly HttpClient _client = factory.CreateClient();

	private CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Completing_a_task_publishes_task_completed_with_its_assignees_at_that_moment()
	{
		var created = await _client.Post("/tasks", new CreateTaskItem.CreateTaskItemRequest("Ship it", "Release the build"));
		var task = await created.Content.ReadAsAsync<CreateTaskItem.CreateTaskItemResponse>();
		var userResponse = await _client.Post("/users", new CreateUser.CreateUserRequest("Ada", $"ada.{Guid.NewGuid():N}@example.com"));
		var user = await userResponse.Content.ReadAsAsync<CreateUser.CreateUserResponse>();
		await _client.Post($"/tasks/{task.Id}/users/{user.Id}");

		await _client.Patch($"/tasks/{task.Id}/completed");

		var message = await ProcessUntilAsync(task.Id);
		Assert.Contains("\"title\":\"Ship it\"", message.Payload);
		Assert.Contains(user.Id.ToString(), message.Payload);
		Assert.NotNull(message.ProcessedAt);
	}

	private Task<OutboxMessage> ProcessUntilAsync(Guid taskId) =>
		factory.ProcessOutboxUntilAsync(
			m => m.Name == "TaskCompletedIntegrationEvent" && m.Payload.Contains(taskId.ToString()),
			m => m.ProcessedAt is not null,
			Ct);
}
#endif
