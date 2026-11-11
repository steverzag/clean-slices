#if (audit)
using System.Text.Json;
using CleanSlices.Api.Features.Tasks;
using CleanSlices.Api.Features.Users;
#if (authentication)
using CleanSlices.Api.Tests.Authentication;
#endif
using CleanSlices.Api.Tests.Infrastructure;
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Infrastructure.Persistence;
using CleanSlices.Infrastructure.Persistence.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Api.Tests.Features.Tasks;

public class AuditTests(ApiFactory factory)
{
	private readonly HttpClient _client = factory.CreateClient();

	[Fact]
	public async Task Every_change_to_a_task_is_recorded_with_its_before_and_after()
	{
		var id = await CreateTaskAsync("Write tests");
		await _client.Patch($"/tasks/{id}/title", new UpdateTitle.UpdateTaskItemTitleRequest("Write more tests"));
		await _client.Patch($"/tasks/{id}/completed");

		var history = await HistoryAsync("TaskItem", id);

		Assert.Equal([AuditAction.Added, AuditAction.Modified, AuditAction.Modified], history.Select(e => e.Action));
		Assert.Null(history[0].Before);
		Assert.Equal("Write tests", Value(history[0].After, "Title"));
		Assert.Equal("Write tests", Value(history[1].Before, "Title"));
		Assert.Equal("Write more tests", Value(history[1].After, "Title"));
		Assert.Equal("Completed", Value(history[2].After, "Status"));
		Assert.All(history, e => Assert.NotNull(e.TraceId));
#if (authentication)
		var userId = await factory.UserIdAsync(TestUsers.User);
		Assert.All(history, e => Assert.Equal(userId, e.UserId));
#else
		Assert.All(history, e => Assert.Null(e.UserId));
#endif
	}

	[Fact]
	public async Task Deleting_a_task_is_recorded()
	{
		var id = await CreateTaskAsync("Short-lived");

		await _client.Delete($"/tasks/{id}");

		var deletion = (await HistoryAsync("TaskItem", id))[^1];
		Assert.Equal("Short-lived", Value(deletion.Before, "Title"));
#if (soft_delete)
		// A soft delete is an update: "who deleted it" comes for free.
		Assert.Equal(AuditAction.Modified, deletion.Action);
		Assert.Null(Value(deletion.Before, "DeletedAt"));
		Assert.NotNull(Value(deletion.After, "DeletedAt"));
#else
		Assert.Equal(AuditAction.Deleted, deletion.Action);
		Assert.Null(deletion.After);
#endif
	}

	[Fact]
	public async Task Properties_marked_not_audited_are_left_out()
	{
		var response = await _client.Post("/users", new CreateUser.CreateUserRequest("Ana", $"{Guid.NewGuid():N}@example.com"));
		var id = (await response.Content.ReadAsAsync<CreateUser.CreateUserResponse>()).Id;

		var created = Assert.Single(await HistoryAsync("User", id));

		using var snapshot = JsonDocument.Parse(created.After!);
		Assert.Equal("Ana", snapshot.RootElement.GetProperty("Name").GetString());
		Assert.False(snapshot.RootElement.TryGetProperty("Email", out _));
	}

	[Fact]
	public async Task A_save_tried_again_after_a_failure_is_recorded_once()
	{
		var id = await CreateTaskAsync("Retried");
		await using (var scope = factory.Services.CreateAsyncScope())
		{
			var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			var task = await context.Set<TaskItem>().SingleAsync(t => t.Id == id, TestContext.Current.CancellationToken);
			var title = context.Entry(task).Property(t => t.Title);

			title.CurrentValue = new string('x', TaskItem.TitleMaxLength + 1);
			await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
			title.CurrentValue = "Retried again";
			await context.SaveChangesAsync(TestContext.Current.CancellationToken);
		}

		var history = await HistoryAsync("TaskItem", id);

		Assert.Equal([AuditAction.Added, AuditAction.Modified], history.Select(e => e.Action));
		Assert.Equal("Retried again", Value(history[1].After, "Title"));
	}

	private async Task<Guid> CreateTaskAsync(string title)
	{
		var response = await _client.Post("/tasks", new CreateTaskItem.CreateTaskItemRequest(title, "Audited"));
		return (await response.Content.ReadAsAsync<CreateTaskItem.CreateTaskItemResponse>()).Id;
	}

	private async Task<List<AuditEntry>> HistoryAsync(string entityType, Guid id)
	{
		await using var scope = factory.Services.CreateAsyncScope();
		var entityId = id.ToString();
		return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<AuditEntry>()
			.Where(e => e.EntityType == entityType && e.EntityId == entityId)
			.OrderBy(e => e.Timestamp).ThenBy(e => e.Id)
			.ToListAsync(TestContext.Current.CancellationToken);
	}

	private static string? Value(string? snapshot, string property)
	{
		using var json = JsonDocument.Parse(snapshot!);
		var value = json.RootElement.GetProperty(property);
		return value.ValueKind == JsonValueKind.Null ? null : value.ToString();
	}
}
#endif
