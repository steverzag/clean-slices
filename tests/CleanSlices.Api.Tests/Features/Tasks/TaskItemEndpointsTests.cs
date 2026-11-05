using System.Collections.Concurrent;
using System.Net;
using CleanSlices.Api.Features.Tasks;
using CleanSlices.Api.Features.Users;
using CleanSlices.Api.Tests.Infrastructure;
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Domain.Users;
using CleanSlices.Core.Messages;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
	public async Task Deleting_a_user_saves_nothing_when_a_domain_event_handler_fails()
	{
		var taskId = await CreateTaskAsync();
		var userId = await CreateUserAsync();
		await _client.Post($"/tasks/{taskId}/users/{userId}");

		await using var failing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
			services.AddScoped<INotificationHandler<UserDeletedDomainEvent>, FailingHandler>()));
		var response = await failing.CreateClient().Delete($"/users/{userId}");

		Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await _client.Get($"/users/{userId}")).StatusCode);
		Assert.Equal(userId, Assert.Single((await GetTaskAsync(taskId)).UserIds));
	}

	[Fact]
	public async Task A_domain_event_handler_that_calls_SaveChanges_is_stopped_and_nothing_is_saved()
	{
		var userId = await CreateUserAsync();
		var recorder = new Recorder();

		await using var saving = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
		{
			services.AddSingleton(recorder);
			services.AddScoped<INotificationHandler<UserDeletedDomainEvent>, SavingHandler>();
		}));
		var response = await saving.CreateClient().Delete($"/users/{userId}");

		Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
		var error = Assert.IsType<InvalidOperationException>(Assert.Single(recorder.Errors));
		Assert.StartsWith("SaveChanges was called while the save pipeline was running", error.Message);
		Assert.Equal(HttpStatusCode.OK, (await _client.Get($"/users/{userId}")).StatusCode);
	}

	[Fact]
	public async Task Events_raised_by_domain_event_handlers_are_dispatched_in_the_same_save()
	{
		var taskId = await CreateTaskAsync();
		var userId = await CreateUserAsync();
		await _client.Post($"/tasks/{taskId}/users/{userId}");
		var recorder = new Recorder();

		// Deleting the user completes their tasks; completing a task raises a new event during the save.
		await using var chained = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
		{
			services.AddSingleton(recorder);
			services.AddScoped<INotificationHandler<UserDeletedDomainEvent>, CompleteTasksOfDeletedUserHandler>();
			services.AddScoped<INotificationHandler<TaskItemCompletedDomainEvent>, RecordCompletedHandler>();
		}));
		var response = await chained.CreateClient().Delete($"/users/{userId}");

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		Assert.Equal(taskId, Assert.Single(recorder.CompletedTaskIds));
		var task = await GetTaskAsync(taskId);
		Assert.Equal(TaskItemStatus.Completed, task.Status);
		Assert.Empty(task.UserIds);
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

	private sealed class FailingHandler : INotificationHandler<UserDeletedDomainEvent>
	{
		public Task Handle(UserDeletedDomainEvent notification, CancellationToken ct) =>
			throw new InvalidOperationException("Simulated failure in a domain event handler.");
	}

	private sealed class Recorder
	{
		public ConcurrentQueue<Exception> Errors { get; } = [];
		public ConcurrentQueue<Guid> CompletedTaskIds { get; } = [];
	}

	private sealed class SavingHandler(AppDbContext context, Recorder recorder) : INotificationHandler<UserDeletedDomainEvent>
	{
		public async Task Handle(UserDeletedDomainEvent notification, CancellationToken ct)
		{
			try
			{
				await context.SaveChangesAsync(ct);
			}
			catch (Exception exception)
			{
				recorder.Errors.Enqueue(exception);
				throw;
			}
		}
	}

	private sealed class CompleteTasksOfDeletedUserHandler(AppDbContext context) : INotificationHandler<UserDeletedDomainEvent>
	{
		public async Task Handle(UserDeletedDomainEvent notification, CancellationToken ct)
		{
			var tasks = await context.Tasks
				.Include(t => t.UserAssignments)
				.Where(t => t.UserAssignments.Any(a => a.UserId == notification.Id))
				.ToListAsync(ct);

			foreach (var task in tasks)
			{
				task.Complete();
			}
		}
	}

	private sealed class RecordCompletedHandler(Recorder recorder) : INotificationHandler<TaskItemCompletedDomainEvent>
	{
		public Task Handle(TaskItemCompletedDomainEvent notification, CancellationToken ct)
		{
			recorder.CompletedTaskIds.Enqueue(notification.Id);
			return Task.CompletedTask;
		}
	}
}
