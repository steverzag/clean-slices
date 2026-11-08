# Realtime

The API pushes messages to connected clients with **SignalR**, through one hub at **`/hubs/notifications`**. Clients connect to the hub; the server sends with `IRealtimeNotifier` (`Infrastructure/Realtime/`):

| Method | Reaches |
|---|---|
| `SendToUserAsync(userId, method, payload)` | every connection of a logged-in user |
| `SendToGroupAsync(group, method, payload)` | the connections that joined a group |
| `SendToAllAsync(method, payload)` | every connection |

`method` is the name the client listens to (`connection.on("TaskChanged", …)` in JavaScript), and `payload` is sent as JSON with camelCase names. The hub and the notifier are in `Api/Configuration/Realtime/`.

## Only changes that are saved

A message about a change is never sent before the change is saved:

- **During a save** (from a domain event handler), the message waits for the commit, and it is dropped if the save fails.
- **Outside a save** (in an endpoint, a background job), it is sent at once.

Notify from a domain event handler, so the message goes out with the change that caused it:

```csharp
internal sealed class NotifyAssigneeHandler(IRealtimeNotifier notifier) : INotificationHandler<TaskAssignedDomainEvent>
{
	public Task Handle(TaskAssignedDomainEvent notification, CancellationToken cancellationToken) =>
		notifier.SendToUserAsync(notification.LoginId, "TaskAssigned", new { notification.TaskId }, cancellationToken);
}
```

Messages are best effort: a client that isn't connected misses them, and nothing is retried. A list of notifications a user can read later is a feature to build (a table), not something this option keeps.

## Users and groups

<!--#if (authentication)-->
The hub requires a login, like every endpoint. SignalR knows a connection's user from the same claim as `ICurrentUser.UserId`, so `SendToUserAsync(currentUser.UserId!, …)` reaches all of that user's connections (several tabs or devices).

Browsers can't send headers on a WebSocket, so the SignalR client sends the token in the query string (`?access_token=…`, set through `accessTokenFactory` in JavaScript). The API reads it there for hub requests only (`UseHubAccessTokens`, before authentication), because a token in a URL can end up in logs, such as a proxy's.

The user ID is the login's ID, not the ID of an entity in your database: to notify the user behind one of your records, store their login ID on it.
<!--#else-->
Without an authentication option there are no users, only groups and everyone: `SendToUserAsync` reaches nobody. Clients join groups through hub methods you write, so any client can join any group (even one named after another user): fine for demos, not for private messages. Add an authentication option for those.
<!--#endif-->

Groups are joined in the hub. Add a method that checks access and adds the connection:

```csharp
public async Task WatchProject(Guid projectId)
{
	// Check that the caller may see this project before joining.
	await Groups.AddToGroupAsync(Context.ConnectionId, $"project:{projectId}");
}
```

A connection leaves its groups when it disconnects; a client that reconnects joins again.

<!--#if (samples)-->
## In the samples

Clients call `WatchTask(taskId)` on the hub to join the task's group (`task:{id}`); `NotifyTaskWatchersHandler` sends `TaskChanged` (`{ taskId, change }`) when the task is completed or deleted. Any client may watch any task: a real app would check access in `WatchTask`.

<!--#endif-->
## Connecting from a browser

```js
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
<!--#if (authentication)-->
	.withUrl("https://localhost:7155/hubs/notifications", { accessTokenFactory: () => token })
<!--#else-->
	.withUrl("https://localhost:7155/hubs/notifications")
<!--#endif-->
	.withAutomaticReconnect()
	.build();

connection.on("TaskChanged", message => console.log(message));
await connection.start();
```

A browser app on another origin (another port counts) needs CORS with credentials, which the API doesn't configure:

```csharp
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
	.WithOrigins("http://localhost:5173")
	.AllowAnyHeader()
	.AllowAnyMethod()
	.AllowCredentials()));

app.UseCors(); // before authentication
```

## Several instances

<!--#if (signalr_redis)-->
With Redis, SignalR uses it as a **backplane**: every instance publishes its messages to Redis, so a client connected to any instance receives them. The channels are prefixed with the app's name (`cleanslices`), so apps sharing a Redis don't hear each other.
<!--#else-->
SignalR keeps connections in the instance that accepted them, so a message sent by one instance only reaches clients connected to it. With several instances, add the Redis option: it brings SignalR's Redis backplane. Azure SignalR Service is the hosted alternative.
<!--#endif-->

## Tests

<!--#if (authentication)-->
The tests connect to the in-memory server over WebSockets with the .NET SignalR client (`Microsoft.AspNetCore.SignalR.Client`), with the token in the query string as browsers send it. They send through `IRealtimeNotifier` from a scope of the test host, and wait for the message with a deadline.
<!--#else-->
The tests connect to the in-memory server over WebSockets with the .NET SignalR client (`Microsoft.AspNetCore.SignalR.Client`). They send through `IRealtimeNotifier` from a scope of the test host, and wait for the message with a deadline.
<!--#endif-->
