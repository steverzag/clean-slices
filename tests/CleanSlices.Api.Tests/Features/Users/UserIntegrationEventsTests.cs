#if (outbox)
using System.Net;
using CleanSlices.Api.Features.Users;
using CleanSlices.Api.Tests.Infrastructure;
using CleanSlices.Api.Tests.Outbox;
#if (inbox)
using CleanSlices.Infrastructure.Inbox;
using CleanSlices.Infrastructure.IntegrationEvents.Users;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
#endif
using CleanSlices.Infrastructure.Outbox;

namespace CleanSlices.Api.Tests.Features.Users;

public class UserIntegrationEventsTests(ApiFactory factory)
{
	private readonly HttpClient _client = factory.CreateClient();

	private CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Creating_a_user_publishes_user_created_with_its_name_and_email()
	{
		var email = $"ana.{Guid.NewGuid():N}@example.com";
		var response = await _client.Post("/users", new CreateUser.CreateUserRequest("Ana", email));
		var user = await response.Content.ReadAsAsync<CreateUser.CreateUserResponse>();

		var message = await ProcessUntilAsync(user.Id, "UserCreatedIntegrationEvent");

		Assert.Contains(email, message.Payload);
		Assert.Contains("\"name\":\"Ana\"", message.Payload);
		Assert.NotNull(message.ProcessedAt);
	}

	[Fact]
	public async Task Deleting_a_user_publishes_users_deleted_with_only_the_id()
	{
		var email = $"luis.{Guid.NewGuid():N}@example.com";
		var response = await _client.Post("/users", new CreateUser.CreateUserRequest("Luis", email));
		var user = await response.Content.ReadAsAsync<CreateUser.CreateUserResponse>();

		var deleted = await _client.Delete($"/users/{user.Id}");
		Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

		var message = await ProcessUntilAsync(user.Id, "users.deleted.v1");
		Assert.DoesNotContain(email, message.Payload);
		Assert.NotNull(message.ProcessedAt);
	}
#if (inbox)

	[Fact]
	public async Task The_welcome_email_isnt_sent_twice_when_its_event_is_delivered_again()
	{
		var response = await _client.Post("/users", new CreateUser.CreateUserRequest("Eva", $"eva.{Guid.NewGuid():N}@example.com"));
		var user = await response.Content.ReadAsAsync<CreateUser.CreateUserResponse>();
		var message = await ProcessUntilAsync(user.Id, "UserCreatedIntegrationEvent");

		// Delivered again, as after a crash between handling and marking the row processed.
		await using (var scope = factory.Services.CreateAsyncScope())
		{
			await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboxMessage>()
				.Where(m => m.Id == message.Id)
				.ExecuteUpdateAsync(set => set.SetProperty(m => m.ProcessedAt, (DateTimeOffset?)null), Ct);
		}

		await ProcessUntilAsync(user.Id, "UserCreatedIntegrationEvent");

		Assert.Equal(1, await WelcomeEmailRecordsAsync(message.Id));
	}

	// Through a broker the handlers run in the background, after the row is processed: wait for the record.
	private async Task<int> WelcomeEmailRecordsAsync(Guid messageId)
	{
		var deadline = DateTime.UtcNow.AddSeconds(30);
		while (true)
		{
			await using var scope = factory.Services.CreateAsyncScope();
			var records = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<ProcessedMessage>()
				.CountAsync(m => m.MessageId == messageId && m.Handler == typeof(SendWelcomeEmailHandler).FullName, Ct);
			if (records > 0 || DateTime.UtcNow >= deadline)
			{
				return records;
			}

			await Task.Delay(50, Ct);
		}
	}
#endif

	private Task<OutboxMessage> ProcessUntilAsync(Guid userId, string name) =>
		factory.ProcessOutboxUntilAsync(m => m.Name == name && m.Payload.Contains(userId.ToString()), m => m.ProcessedAt is not null, Ct);
}
#endif
