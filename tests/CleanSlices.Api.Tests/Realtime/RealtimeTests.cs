#if (signalr)
using System.Text.Json;
//#if (samples)
using System.Net;
using CleanSlices.Api.Features.Tasks;
//#endif
using CleanSlices.Api.Configuration.Realtime;
#if (authentication)
using CleanSlices.Api.Tests.Authentication;
#endif
using CleanSlices.Api.Tests.Infrastructure;
//#if (samples)
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Messages;
using CleanSlices.Infrastructure.EventHandlers.TaskItems;
//#endif
using CleanSlices.Infrastructure.Realtime;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
//#if (samples)
using Microsoft.AspNetCore.TestHost;
//#endif
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Api.Tests.Realtime;

// The tests connect over WebSockets to the in-memory server, with the token in the query string as browsers send it.
public class RealtimeTests(ApiFactory factory) : IAsyncDisposable
{
	private const string Probe = "Probe";
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

	private readonly List<HubConnection> _connections = [];

	[Fact]
	public async Task A_message_sent_outside_a_save_arrives_at_once()
	{
		var connection = await ConnectAsync(factory);
		var id = Guid.NewGuid();
		var received = Receive(connection, Probe, id);

		await SendAsync(factory, notifier => notifier.SendToAllAsync(Probe, new { id }));

		await received.WaitAsync(Timeout, TestContext.Current.CancellationToken);
	}
	//#if (samples)

	[Fact]
	public async Task Watchers_of_a_task_hear_when_it_is_completed()
	{
		var client = factory.CreateClient();
		var taskId = await CreateTaskAsync(client);
		var connection = await ConnectAsync(factory);
		await connection.InvokeAsync("WatchTask", taskId, TestContext.Current.CancellationToken);
		var received = Receive(connection, TaskWatchers.TaskChanged, taskId, "taskId");

		await client.Patch($"/tasks/{taskId}/completed");

		var message = await received.WaitAsync(Timeout, TestContext.Current.CancellationToken);
		Assert.Equal("Completed", message.GetProperty("change").GetString());
	}

	[Fact]
	public async Task Nothing_is_sent_when_the_save_fails()
	{
		await using var failing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
			services.AddScoped<INotificationHandler<TaskItemCompletedDomainEvent>, FailingHandler>()));
		var client = failing.CreateClient();
		var taskId = await CreateTaskAsync(client);
		var connection = await ConnectAsync(failing);
		await connection.InvokeAsync("WatchTask", taskId, TestContext.Current.CancellationToken);
		var received = Receive(connection, TaskWatchers.TaskChanged, taskId, "taskId");

		var response = await client.Patch($"/tasks/{taskId}/completed");

		Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
		await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
		Assert.False(received.IsCompleted);
	}
	//#endif
#if (authentication)

	[Fact]
	public async Task A_connection_without_a_login_is_refused()
	{
		await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(factory, login: false));
	}

	[Fact]
	public async Task A_message_to_a_user_reaches_their_connection()
	{
		var connection = await ConnectAsync(factory);
		var userId = await factory.UserIdAsync(TestUsers.User);
		var id = Guid.NewGuid();
		var received = Receive(connection, Probe, id);

		await SendAsync(factory, notifier => notifier.SendToUserAsync(userId, Probe, new { id }));

		await received.WaitAsync(Timeout, TestContext.Current.CancellationToken);
	}
#endif
#if (signalr_redis)

	[Fact]
	public async Task A_message_sent_by_another_instance_arrives()
	{
		var connection = await ConnectAsync(factory);
		await using var other = factory.AnotherInstance();
		var id = Guid.NewGuid();
		var received = Receive(connection, Probe, id);

		await SendAsync(other, notifier => notifier.SendToAllAsync(Probe, new { id }));

		await received.WaitAsync(Timeout, TestContext.Current.CancellationToken);
	}
#endif

	private async Task<HubConnection> ConnectAsync(WebApplicationFactory<Program> host, bool login = true)
	{
#if (authentication)
		var token = login ? await factory.LoginAsync(TestUsers.User) : null;
#endif
		var server = host.Server;
		var connection = new HubConnectionBuilder()
			.WithUrl(new Uri(server.BaseAddress, RealtimeRegistration.NotificationsHubPath), options =>
			{
				options.Transports = HttpTransportType.WebSockets;
				options.SkipNegotiation = true;
				options.WebSocketFactory = async (context, cancellationToken) =>
				{
					var uri = context.Uri;
#if (authentication)
					if (token is not null)
					{
						uri = new UriBuilder(uri) { Query = $"access_token={Uri.EscapeDataString(token)}" }.Uri;
					}
#endif
					return await server.CreateWebSocketClient().ConnectAsync(uri, cancellationToken);
				};
			})
			.Build();
		_connections.Add(connection);

		await connection.StartAsync(TestContext.Current.CancellationToken);
		return connection;
	}

	// Other tests send at the same time: only this test's message counts.
	private static Task<JsonElement> Receive(HubConnection connection, string method, Guid id, string idProperty = "id")
	{
		var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
		connection.On<JsonElement>(method, message =>
		{
			if (message.GetProperty(idProperty).GetGuid() == id)
			{
				received.TrySetResult(message);
			}
		});
		return received.Task;
	}

	private static async Task SendAsync(WebApplicationFactory<Program> host, Func<IRealtimeNotifier, Task> send)
	{
		using var scope = host.Services.CreateScope();
		await send(scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>());
	}
	//#if (samples)

	private static async Task<Guid> CreateTaskAsync(HttpClient client)
	{
		var response = await client.Post("/tasks", new CreateTaskItem.CreateTaskItemRequest("Watched", "A task with watchers"));
		return (await response.Content.ReadAsAsync<CreateTaskItem.CreateTaskItemResponse>()).Id;
	}

	private sealed class FailingHandler : INotificationHandler<TaskItemCompletedDomainEvent>
	{
		public Task Handle(TaskItemCompletedDomainEvent notification, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("The save fails after the notification was queued.");
	}
	//#endif

	public async ValueTask DisposeAsync()
	{
		foreach (var connection in _connections)
		{
			await connection.DisposeAsync();
		}

		GC.SuppressFinalize(this);
	}
}
#endif
