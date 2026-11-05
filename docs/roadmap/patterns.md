# Patterns

A pattern changes *how code is written*: method signatures, control flow, how a slice reaches the database or the outside world. Any number can be selected, and each one changes what `add feature` generates. See the [catalog index](README.md).

## Result&lt;T&gt;

**What.** Domain methods return `Result` / `Result<T>` instead of throwing for expected failures. Errors are `ValidationError`, `BusinessRuleError`, `NotFoundError` and `ConflictError`. Endpoints chain results with `Map` / `Tap`, and `ToProblem()` turns a failure into ProblemDetails. Bugs still throw and become 500s.

**Design.**

- Our own `Result.cs`, no library (ErrorOr, FluentResults), so generated solutions have no extra dependency.
- Rule: the same failure gives the same HTTP response with or without the pattern (already true on `main` and `pattern/result`).
- Today it's the `pattern/result` branch. It moves to template conditionals: `Result.cs`, `Error.cs` and `ResultExtensions.cs` exist only with the pattern; `Exceptions.cs` and the exception mapping only without it; inline blocks cover entity methods and handler bodies.

**Feature template.** Entity methods return results; handlers chain them and call `ToProblem()`.

**Interactions.**

| With | Effect |
|---|---|
| Repository | Repositories return the entity or `null`; the handler turns `null` into `NotFoundError`. |
| CQRS | Handlers return `Result` / `Result<T>`; the endpoint calls `ToProblem()`. |
| Request validation | Invalid requests are rejected before the handler, with the same response shape. |

## Outbox

**What.** Integration events are stored as rows in an `OutboxMessages` table **in the same transaction** as the data, and a background `OutboxProcessor` delivers them. Without the outbox, an integration event is sent right after the commit and is lost if the process dies at that moment.

**Design.**

- `IIntegrationEventPublisher` (from the [integration events](hidden-units.md#integration-events) unit) writes an outbox row instead of buffering the event.
- The processor reads pending rows and hands each one to `IMessageDispatcher`: in-process by default, the broker with a queue option.
- Serialization: JSON plus a type *name* resolved through a registry built at startup. Never `Type.GetType` on a string from the database. Events are plain records.
- Several API instances: each row is claimed with a lease (`LockedUntil`) through EF `ExecuteUpdate`, which works on every provider. No `UPDLOCK READPAST` / `SKIP LOCKED`, which would need glue per database.
- Failures: retries with backoff, `Attempts` and `LastError` kept; after `MaxAttempts` (default 5) the row is marked failed and left for inspection.
- Clean-up: processed rows are deleted after `Retention` (default 7 days).
- The `traceparent` is stored, so processing links back to the original request in the Aspire dashboard.
- Settings: `Outbox:PollInterval`, `BatchSize`, `MaxAttempts`, `Retention`.
- Migrations stay on the user's side (rule 5).

**Feature template.** No change.

**Interactions.** Requires integration events (added automatically). Queue options provide the dispatcher, with no glue. The inbox removes duplicates on the receiving side.

**Trade-offs to document.** Delivery is asynchronous and **at least once**: integration event handlers must tolerate running twice, or the inbox must be selected.

## Inbox

**What.** Makes integration event handlers run once per message, even when a message is delivered twice.

**Design.**

- A decorator records `(MessageId, Handler)` in a `ProcessedMessages` table and skips messages already processed.
- The record is committed **together with the handler's own changes**, so it's exactly-once in effect.
- Only available when something delivers at least once: the outbox or a queue option provides that capability. Selected by default when available.

**Feature template.** No change.

## Repository / unit of work

**What.** Handlers use repositories and a unit of work defined in Core instead of `AppDbContext`.

**Design.**

- Each entity gets `IWidgetRepository : IRepository<Widget>`, with a generic `Repository<T>` base implementation. Common methods come for free; entity-specific queries go in the per-entity pair.
- Interfaces in Core, implementations in Infrastructure, registered by scanning. The Api stops using `AppDbContext`.
- `IUnitOfWork.SaveChangesAsync` is implemented directly by `AppDbContext`. Repositories never save; the handler ends the transaction. The save pipeline keeps working unchanged.
- `IRepository<T>` has a paged list method returning the base template's page type.

**Feature template.** Generates an empty `IWidgetRepository` and `WidgetRepository`; handlers take the repository and `IUnitOfWork`.

```csharp
public async Task<IResult> Handler(IWidgetRepository widgets, IUnitOfWork unitOfWork, Guid id, UpdateWidgetNameRequest request, CancellationToken cancellationToken)
{
	var entity = await widgets.GetByIdAsync(id, cancellationToken)
		?? throw WidgetErrors.NotFound(id);

	entity.UpdateName(request.Name);
	await unitOfWork.SaveChangesAsync(cancellationToken);

	return Results.NoContent();
}
```

**Interactions.** With CQRS, queries skip repositories (see below). Soft delete's query filter applies inside repositories.

**Trade-offs to document.** Without CQRS, reads return entities and the endpoint maps them, so whole rows are loaded instead of projecting only the columns needed.

## CQRS

**What.** Commands (change state) and queries (read) are separate classes with their own handlers, on the same database. A separate read store is out of scope.

**Design.**

- Handlers live in the slice file, next to the endpoint. There is no Application project.
- Handlers are registered by scanning.
- Commands return the new ID or nothing.
- With Repository: commands use repositories and `IUnitOfWork`; queries use a read-only `IReadDb` (`IQueryable<T> Query<T>()`, always no-tracking) and keep their `Select(...)` projections. Without Repository: both use `AppDbContext`.
- Domain events are unchanged.

**Sub-options.** `dispatcher`:

| Value | Handlers | Notes |
|---|---|---|
| `handlers` (default) | Our own `ICommandHandler<TCommand>` / `IQueryHandler<TQuery, TResult>`, injected into the endpoint; cross-cutting steps as decorators | No package |
| `mediator` | `IRequest` / `IRequestHandler` / `ISender` / `IPipelineBehavior` from the source-generated `Mediator` package (MIT) | `ValueTask` |
| `mediatr` | The same shape with MediatR | Commercial license from version 13: `MediatR:LicenseKey` setting generated empty, notice printed (rule 9) |

`mediator` and `mediatr` share one code shape; they differ in `Task` / `ValueTask` and registration.

**Feature template.** Each slice file holds the request, the command or query, its handler and the endpoint.

**Interactions.**

| With | Effect |
|---|---|
| Result | Handlers return results; decorators short-circuit with a failure instead of throwing. |
| Request validation | Runs at the endpoint, not as a decorator or pipeline behavior. |
| Idempotency | Works at the endpoint, whatever the dispatcher. |

## Idempotency keys

**What.** A client sends `Idempotency-Key: <uuid>`; a retry with the same key gets the stored response instead of running the action again.

**Design.**

- Behavior follows the IETF draft:

  | Situation | Response |
  |---|---|
  | Same key, same request, finished | The stored response is replayed |
  | Same key, different body | 422 |
  | Same key, first request still running | 409 |
  | Missing key where required | 400 |

- Opt-in per endpoint with `.WithIdempotency()`, an endpoint filter. The key is optional unless `Idempotency:Required` is `true`.
- Records are stored through `IDistributedCache`: in memory by default, Redis when the Redis option is selected, with no glue. A lock per key inside the instance gives the 409.
- The request fingerprint is a SHA-256 hash of method, path and body. Records are kept for 24 hours (configurable).
- With an authentication option, keys are scoped per user through `ICurrentUser`.

**Feature template.** `.WithIdempotency()` on the Create endpoint.

**Interactions.** Request validation runs first, so an invalid request stores no key.

**Trade-offs to document.** In memory, keys are lost on a crash or restart, so a retry right after one runs again. With several instances behind Redis, two retries with the same key reaching different instances within milliseconds could both run. A database-backed store with the transactional guarantee is parked as a possible `store` sub-option.

## Request validation

**What.** FluentValidation validators check the *shape* of requests before the handler runs.

**Design.**

- Request validation checks what the domain can't: missing or null fields, values that can't be parsed, requests that don't map to an entity. **The domain stays the authority** for business rules, and limits reuse domain constants (`Widget.NameMaxLength`), so each number is defined once.
- FluentValidation only (Apache 2.0). A validator class sits next to the request in the slice file; a generic endpoint filter runs it.
- Always at the endpoint, even with CQRS. One location keeps the feature template simple, and the domain still checks every business rule wherever a command comes from.
- Responses use the same `ValidationProblemDetails` shape, with camelCase field names, as domain validation (rule 7).
- Order on an endpoint: validation, then idempotency.

**Feature template.** `CreateWidget` and `UpdateWidgetName` validate `Name`: required, at most `Widget.NameMaxLength`.

**Trade-offs to document.** For simple CRUD on one entity it adds little; its value grows with requests that don't match an entity. MediatR users may expect a `ValidationBehavior`; this template doesn't use one, on purpose.
