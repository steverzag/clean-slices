# Patterns

A pattern changes *how code is written*: method signatures, control flow, how a slice reaches the database or the outside world. Any number can be selected, and each one changes what `add feature` generates. See the [catalog index](README.md).

## Result&lt;T&gt;

**What.** Domain methods return `Result` / `Result<T>` instead of throwing for expected failures. Errors are `ValidationError`, `BusinessRuleError`, `NotFoundError` and `ConflictError`. Endpoints chain results with `Map` / `Tap`, and `ToProblem()` turns a failure into ProblemDetails. Bugs still throw and become 500s.

**Design.**

- Our own `Result.cs`, no library (ErrorOr, FluentResults), so generated solutions have no extra dependency.
- Rule: the same failure gives the same HTTP response with or without the pattern. The API tests are the same for both variants and pass on both.
- **Built (phase 2)** as template conditionals: `Result.cs`, `Error.cs`, `ResultExtensions.cs` and `ResultAssertions.cs` exist only with the option; `Exceptions.cs` and the exception mapping only without it; `#if (result)` blocks cover entity methods, handlers and the Core tests. `docs/error-handling.md` has a variant per option. The output was checked to be identical to the former `pattern/result` branch.

**Feature template.** Entity methods return results; handlers chain them and call `ToProblem()`.

**Samples.** Every sample entity and endpoint uses results. They show what Widget can't: business rules, conflicts (email in use), validation of several fields, and all-or-nothing chains (assign and unassign users).

**Interactions.**

| With | Effect |
|---|---|
| Repository | Repositories return the entity or `null`; the handler turns `null` into `NotFoundError`. |
| CQRS | Handlers return `Result` / `Result<T>`; the endpoint calls `ToProblem()`. |
| Request validation | Invalid requests are rejected before the handler, with the same response shape. |

## Outbox

**What.** Integration events are stored as rows in an `OutboxMessages` table **in the same transaction** as the data, and a background `OutboxProcessor` delivers them. Without the outbox, an integration event is sent right after the commit and is lost if the process dies at that moment.

**Built (phase 4).** `Infrastructure/Outbox/`; documented for users in `docs/outbox.md`.

**Design.**

- `OutboxPublisher` implements `IIntegrationEventPublisher` (from the [integration events](hidden-units.md#integration-events) unit): it adds an `OutboxMessage` row to the current save, with `Activity.Current?.Id` as the traceparent. Publishing from a domain event handler (which runs before the commit) puts the row in the same transaction as the data.
- `OutboxProcessor` hands each row to `IMessageDispatcher`: in-process by default, the broker with a queue option. `OutboxWorker` is the background loop that calls it; tests remove the worker and call the processor themselves.
- **Claiming, provider-neutral** (no `UPDLOCK READPAST` / `SKIP LOCKED`): select the ids of up to `BatchSize` pending rows, then `ExecuteUpdate` the rows *that are still pending* among them, setting `LockedUntil` / `LockedBy`. The pending conditions must be in the update's own `WHERE`, where the database re-checks them on each locked row. A single `ExecuteUpdate` with `OrderBy` + `Take` doesn't work: EF translates it as an update joined to a `TOP n` subquery, which checks the conditions only in the subquery, and two processors could claim the same row (found by the CI matrix, fixed in phase 4).
- Failures: `Attempts` and `LastError` kept, retried after `RetryDelay × 2^(attempt−1)`; after `MaxAttempts` the row is marked failed and left for inspection. An unknown event name or an unreadable payload fails at once, since retrying can't fix it.
- Clean-up: processed rows are deleted after `Retention`, hourly.
- Tracing: processing starts a consumer activity parented to the stored traceparent, from an `ActivitySource` named after the application, the source ServiceDefaults already exports. No ServiceDefaults edit; the same convention serves later options.
- Settings, defaults in code: `Outbox:PollInterval` (2 s), `BatchSize` (20), `MaxAttempts` (5), `RetryDelay` (5 s), `LeaseDuration` (2 min), `Retention` (7 days).
- Catalog: requires `integration-events`, provides `at-least-once-delivery`, `addsTables` (the CLI prints the migration after `add outbox`). Migrations stay on the user's side (rule 5).

**Feature template.** No change.

**Samples.** Three integration events, published from the samples' domain events: `UserCreated` (a welcome email), `TaskCompleted` (notifying the assignees it had then) and `users.deleted.v1` (an id-only event with a deliberate name). See `docs/samples.md` in a generated solution. Tests check what's published and processed.

**Tests.** `tests/…Api.Tests/Outbox/`, with a test-only event and handlers: a pending row with the trace, nothing without a save, delivered once, retried then failed (other handlers keep running), an unknown event failed at once, retention, and parallel processors handling each message once (a sanity check: it couldn't reproduce the claim race, which depends on timing).

**Interactions.** Requires integration events (added automatically). Queue options provide the dispatcher, with no glue. The inbox removes duplicates on the receiving side and is recommended with the outbox.

**Trade-offs to document.** Delivery is asynchronous and **at least once**: integration event handlers must tolerate running twice, or the inbox must be selected. No ordering guarantee once a row is retried. Polling queries the table every `PollInterval`. Later idea: outbox metrics (pending and failed rows).

## Inbox

**What.** Makes integration event handlers run once per message, even when a message is delivered twice.

**Built (phase 4).** `Infrastructure/Inbox/`; documented for users in `docs/inbox.md`.

**Design.**

- `InboxFilter`, an `IIntegrationEventHandlerFilter` (the integration events unit's hook around every handler call), records `(MessageId, Handler)` in `ProcessedMessages`.
- Per handler: if the record exists, the handler is skipped. Otherwise the record is added to the handler's `DbContext` *before* it runs, and saved *with* its changes after. No explicit transaction (Aspire's retrying execution strategy makes them awkward); the rule "an integration event handler saves at most once, at the end" makes one unnecessary.
- One record per handler, so a redelivery after one handler failed skips those that succeeded.
- Concurrent duplicates (an expired lease): the primary key makes the second save fail, and the filter asks again whether the record exists. Provider-neutral, no error numbers.
- Clean-up: records older than `Inbox:Retention` (7 days) are deleted hourly by `InboxCleanupWorker`. Not in the original design; without it the table grows forever.
- Catalog: requires `integration-events` and `at-least-once-delivery` (the outbox or a queue option), `recommended` (offered when the selection makes it available, never added silently), `addsTables`.

**Feature template.** No change.

**Samples.** No code of its own: it deduplicates the samples' integration event handlers. A sample test delivers the welcome email's event again and checks `SendWelcomeEmailHandler` is recorded once.

**Tests.** `tests/…Api.Tests/Inbox/`: a message delivered twice is handled once, a redelivery retries only the handler that failed, two simultaneous deliveries record once without failing, retention. With the inbox, the outbox's retry test expects the succeeding handler to run once instead of on every attempt.

**Phase 7 notes.** When a queue provides at-least-once delivery without the outbox: the inbox tests reuse the outbox's test event, and the test host removes the inbox's clean-up worker inside the outbox's `#if`. Both must stand on their own then.

## Repository / unit of work

**What.** Handlers use repositories and a unit of work defined in Core instead of `AppDbContext`.

**Design.**

- Each entity gets `IWidgetRepository : IRepository<Widget>`, with a generic `Repository<T>` base implementation. Common methods come for free; entity-specific queries go in the per-entity pair.
- Interfaces in Core, implementations in Infrastructure, registered by scanning. The Api stops using `AppDbContext`.
- `IUnitOfWork.SaveChangesAsync` is implemented directly by `AppDbContext`. Repositories never save; the handler ends the transaction. The save pipeline keeps working unchanged.
- `IRepository<T>` has a paged list method. The base template's `PaginationList<T>` lives in Infrastructure, which Core can't reference, so this option adds a Core page type or moves it.

**Feature template.** Generates an empty `IWidgetRepository` and `WidgetRepository`; handlers take the repository and `IUnitOfWork`.

**Samples.** *Proposed:* `IUserRepository` with an email lookup (the `EmailInUse` check) and `ITaskItemRepository` loading assignments: what belongs in a specific repository, beyond the generic one.

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

**Samples.** *Proposed:* every sample endpoint as a command or a query; assigning a user shows a command that loads two entities, and the lists show queries through `IReadDb`.

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

**Samples.** *Proposed:* `.WithIdempotency()` on the creates and on assigning a user, an action that isn't naturally idempotent.

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

**Samples.** *Proposed:* a validator for every sample request, including the email's shape, reusing the domain's length constants.

**Trade-offs to document.** For simple CRUD on one entity it adds little; its value grows with requests that don't match an entity. MediatR users may expect a `ValidationBehavior`; this template doesn't use one, on purpose.
