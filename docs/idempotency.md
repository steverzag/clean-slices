# Idempotency keys

A client that doesn't know whether a request went through (a timeout, a dropped connection) can send it again safely. It sends an `Idempotency-Key` header with a value of its own, usually a UUID, and uses the same value for every retry. The first request runs. A retry with the same key gets the **stored response**, and the action doesn't run again.

## Using it on an endpoint

Idempotency is opt-in per endpoint:

```csharp
app.MapPost("/products", Handler)
	.WithTags("Products")
	.WithIdempotency()
	.WithName(nameof(CreateProduct));
```

Use it where running a request twice does harm: creates, payments, sending something. Updates that set a value (`PATCH …/name`) and deletes are naturally idempotent and don't need it.

## What clients see

| Situation | Response |
|---|---|
| First request with a key | The endpoint runs. If it succeeds, its response is stored. |
| Same key, same request, after it finished | The stored response: same status, `Location`, body, plus `Idempotent-Replayed: true` |
| Same key, a different request (other body, route or query) | 422 |
| Same key, while the first request is still running | 409, retry later |
| No key | The endpoint runs as usual, or 400 when keys are required (see settings) |
| A key that is empty, repeated or longer than 255 characters | 400 |

- **Only successful responses (2xx) are stored.** After a failure (an invalid request, a conflict, a server error) a retry with the same key runs again, so the client can fix the request or wait and retry.
- "The same request" means the same method, path, query and body values, compared through a SHA-256 hash. Injected services and the cancellation token don't count.
- Keys belong to a user (`ICurrentUser.UserId`): the same key from two users is two keys. Until an authentication option is added, every request is anonymous and shares one space, so clients must use unique keys (UUIDs).

## Settings

In the `Idempotency` section of `appsettings.json`; the defaults are in `Configuration/Idempotency/IdempotencyOptions.cs`.

| Setting | Default | |
|---|---|---|
| `Required` | `false` | `true` makes the key mandatory on every endpoint with `.WithIdempotency()` |
| `Retention` | 24 hours | How long a stored response is replayed |

## Where responses are stored

In `IDistributedCache`, which is **in memory** in this solution. The Redis option replaces it with a shared Redis cache, and idempotency uses it with no change.

The filter (`Configuration/Idempotency/Idempotency.cs`) runs the endpoint's result into a buffer, stores status, `Content-Type`, `Location` and body, then sends the same bytes to the client.

## Trade-offs

- **In memory, keys are lost on a restart or a crash.** A retry right after one runs again. With several instances, each has its own memory: use Redis.
- **The 409 only covers one instance.** With several instances behind Redis, two retries with the same key that reach different instances within milliseconds can both run.
- **A crash between the save and storing the response** leaves the action done but not recorded, so a retry runs it again. Closing that gap needs the response stored in the same database transaction as the data, which this solution doesn't do.
- Stored responses can contain personal data (whatever the endpoint returns) for the retention period.
