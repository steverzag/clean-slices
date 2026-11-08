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

**Phase 7 notes (resolved in 7b).** With a queue providing at-least-once delivery, the inbox works without the outbox: its tests call the handler runner directly (the inbox wraps the handlers whatever the transport), the test event moved to `Api.Tests/IntegrationEvents/TestEvents.cs`, and the test host removes the inbox's clean-up worker under the inbox's own condition. The matrix runs `rabbitmq+inbox` (no outbox) on every push.

## Repository / unit of work

**What.** Handlers use repositories and a unit of work defined in Core instead of `AppDbContext`.

**Built (phase 6).** Documented for users in `docs/repository.md`.

**Design.**

- Each entity gets `IWidgetRepository : IRepository<Widget>` (next to the entity, `Core/Domain/Widgets/`), with a generic `Repository<T>` base implementation (`Infrastructure/Persistence/Repositories/`). Common methods come for free; entity-specific queries go in the per-entity pair.
- `IRepository<T>` and `IUnitOfWork` sit in `Core/Domain`, next to `Entity`, so slices need no extra namespace for them. `IRepository<T>`: `GetByIdAsync` (tracked, `FindAsync`), `ExistsAsync`, `GetPageAsync` (no tracking, newest first), `Add`, `Remove`. No `IQueryable`.
- Implementations are registered by scanning: every class implementing an interface derived from `IRepository<>` is registered as that interface. A test checks that every repository interface in Core resolves, and that `IUnitOfWork` is the scope's `AppDbContext`.
- `IUnitOfWork.SaveChangesAsync` is implemented directly by `AppDbContext` (its declaration line is a conditional). Repositories never save; the handler ends the work. The save pipeline keeps working unchanged.
- **The Api stops using `AppDbContext`**: every template and sample endpoint, reads included. Infrastructure code (event handlers, publishers) keeps using it.
- **`Page<T>` in `Core/Shared`**: a record with the same properties, in the same order, as the Api's `PaginationResponse<T>`, so list responses are byte-for-byte the same JSON with or without the option (rule 7); `Map` turns entities into response items. `Repository<T>.GetPageAsync` reuses the base `PaginateAsync`; the base pagination types are untouched.
- Slices follow the flat-chain rule: each handler is four complete members (`repository && result`, `repository`, `result`, default); lists are two (`result` doesn't change them). Usings: EF Core and `Infrastructure.Persistence` under `#if (!repository)`; the domain namespace under `#if (repository || validation)` where only validators needed it before. The chains were generated by a script from the existing members, then the reads were rewritten by hand.

**Feature template.** Generates an empty `IWidgetRepository` and `WidgetRepository` (excluded without the option by the feature template's own `sources` modifier); handlers take the repository and `IUnitOfWork`.

**Samples.** `IUserRepository.IsEmailInUseAsync` (two overloads: create, and update excluding the user) and `ITaskItemRepository.GetWithAssignmentsAsync` (assign, unassign, and the task read with its user IDs): what belongs in a specific repository, beyond the generic one. `AssignUser` takes two repositories.

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

**Built (phase 6).** `Api/Configuration/Cqrs/`, `Core/Domain/IReadDb.cs`; documented for users in `docs/cqrs.md`. The first option with a choice.

**Design.**

- The command or query and its handler are **nested in the endpoint class**, next to the request (`UpdateWidgetName.UpdateWidgetNameCommand`, `UpdateWidgetName.UpdateWidgetNameHandler`). There is no Application project. Endpoints take only `ISender` plus their route and body values.
- Handlers are registered by scanning (own handlers) or by Mediator's source generator, with a scoped lifetime (Mediator's default is singleton; handlers use the request's `AppDbContext`).
- **Commands return `Unit`, or what the endpoint answers** (changed while building: the plan said "the new ID or nothing"). Creates return their response record, because `CreateTaskItemResponse` has `Status` and `CreatedAt`: returning only the ID would need a second read or change the response (rule 7).
- **Queries always read through `IReadDb`** (`IQueryable<T> Query<T>()`, no tracking, implemented by `AppDbContext`), with or without the repository option (changed while building: the plan had queries on `AppDbContext` without Repository). Queries can't save by construction, and query handlers don't vary with Repository, which halves their variants. They keep their `Select(...)` projections. Commands use repositories and `IUnitOfWork` with Repository, `AppDbContext` without.
- With `result`, handlers return `Result` / `Result<T>` (`Result.Failure(...)` for failures, since `Result` has no implicit conversion from an error) and endpoints map them as before; without it, handlers throw the same exceptions as before.
- No decorators or pipeline behaviors (rule 10: nothing uses one). Request validation and idempotency stay at the endpoint.
- Domain events are unchanged.
- Slice shape: command record (`result` / not), four handler variants (`cqrs` × repository × `result`), and two endpoint members (`cqrs && result`, `cqrs`) at the top of the existing endpoint chain, all flat. Queries: two handler variants (by id) or one (lists). The command code was generated from the existing endpoint members by a script, then reviewed; the queries were written by hand.
- Mediator's packages are XML conditionals, so in this repository its generator runs in every build; it generates nothing harmful when no message uses its interfaces. A message without a handler is a build error with Mediator (`MSG0005`, warnings as errors), a run-time error with own handlers.

**Sub-options.** `dispatcher` (decided in phase 6, after a prototype):

| Value | Handlers | Notes |
|---|---|---|
| `handlers` (default) | Our own `ICommand` / `ICommand<T>` / `ICommandHandler<…>` / `ISender.Send` / `Unit`, with **Mediator's vocabulary** and `ValueTask`, and a small `ISender` of our own | No package |
| `mediator` | The same interfaces from the source-generated `Mediator` package (MIT) | MIT, checked in phase 6 (version 3.0.2) |

**MediatR is dropped** for now: it's commercial since version 13, and its vocabulary (`IRequest`, `Task`) can't match the other two. Because own handlers and Mediator share one vocabulary, **slice code is identical for both**: slices name no dispatcher namespace, and one `global using` file per dispatcher picks it. The choice only changes that file, registration and the package.

**The prototype** (`UpdateWidgetName` in every shape, compiled in every combination):

| | Files | Lines | Directive lines | Places to change "not found" |
|---|---|---|---|---|
| 16 shapes (with MediatR), small nested blocks | 1 | 183 | 73 (40 %), nested 3 deep | 6 |
| 16 shapes, one file per combination | 16 | 656 | 2 each | 16 |
| **8 shapes** (Mediator vocabulary, no MediatR), flat `#elif` chains of complete members | **1** | **159** | **23 (14 %)**, no nesting inside members | 8 |
| 8 shapes, one file per combination | 8 | 302 | 2 each | 8 |

Both 8-shape versions contain the same duplicated code (8 handler bodies, the inherent cost of three options meeting in one handler); the single file keeps them side by side and needs no rename machinery. So: one file per slice, flat chains (see CONTRIBUTING, *Several options in one member*), and one file per combination only as a fallback. The matrix gets an explicit job for `result` + repository + CQRS.

**Feature template.** Each slice file holds the request, the command or query, its handler and the endpoint: three commands (create, update name, delete) and two queries (by id, list).

**Samples.** Every sample endpoint is a command or a query (13 commands, 4 queries). `AssignUser` is a command whose handler uses two repositories (or `AppDbContext`); the lists and the reads by id are queries through `IReadDb`. Tests: `Api.Tests/Configuration/SenderTests.cs` checks the own dispatcher (command with and without a response, query, missing handler); every endpoint test runs through `ISender` with the option.

**Interactions.**

| With | Effect |
|---|---|
| Result | Handlers return results; endpoints turn them into responses as before. |
| Request validation | Runs at the endpoint, not as a decorator or pipeline behavior. |
| Idempotency | Works at the endpoint, whatever the dispatcher. |

## Idempotency keys

**What.** A client sends `Idempotency-Key: <uuid>`; a retry with the same key gets the stored response instead of running the action again.

**Built (phase 6).** `Api/Configuration/Idempotency/`; requires the [`current-user`](hidden-units.md#current-user) and [`cache`](hidden-units.md#cache) hidden units. Documented for users in `docs/idempotency.md`.

**Design.**

- Behavior follows the IETF draft:

  | Situation | Response |
  |---|---|
  | Same key, same request, finished | The stored response is replayed, with `Idempotent-Replayed: true` |
  | Same key, different request | 422 |
  | Same key, first request still running | 409 |
  | Missing key where required | 400 |
  | Empty, repeated or longer than 255 characters | 400 |

- Opt-in per endpoint with `.WithIdempotency()`, an endpoint filter factory. The key is optional unless `Idempotency:Required` is `true`. Settings have their defaults in code (`IdempotencyOptions`); appsettings stay untouched.
- **The response is captured by running the endpoint's `IResult` into a buffer** (the response body swapped for a `MemoryStream`): status, `Content-Type`, `Location` and body are stored, and the stored record is itself an `IResult` that writes them, for the first response and every replay alike.
- **Only 2xx responses are stored.** A failure releases the key, so a retry runs again. This also keeps rule 7: with or without `result`, a failure gives the same response, whether it's an exception (handled outside the filter) or a problem result.
- Records go to `IDistributedCache`: in memory, Redis when the Redis option is selected, with no glue. A set of keys in progress inside the instance gives the 409.
- **The fingerprint covers the bound arguments, not the raw body:** SHA-256 of method, path, query and the JSON of the handler's arguments that aren't services, the cancellation token or the `HttpContext` family. The factory picks those arguments once, at startup. Reading the raw body would need buffering enabled before model binding, in a middleware; equivalent JSON with different whitespace now counts as the same request. Records are kept for 24 hours (`Idempotency:Retention`).
- Keys are scoped per user through `ICurrentUser` (anonymous for now: one shared space).

**Feature template.** `.WithIdempotency()` on the Create endpoint; a test retries a create with the same key and gets the same widget.

**Samples.** `.WithIdempotency()` on `CreateUser` and `CreateTaskItem`. Assigning a user, proposed earlier, is already idempotent (a set add), so it doesn't get it. A test shows the point: a user creation retried with the same key gets the first 201 instead of a 409 for the email.

**Tests.** `Api.Tests/Configuration/IdempotencyTests.cs` runs the filter on a small `TestServer` app of its own (no database): replay, 422, 409 while running, failures not stored, no key, required key, invalid key, keys per user.

**Interactions.** Request validation runs first (a group filter); an invalid request fails, so it stores nothing either way.

**Trade-offs to document.** In memory, keys are lost on a crash or restart, so a retry right after one runs again. With several instances behind Redis, two retries with the same key reaching different instances within milliseconds could both run. A crash between the save and storing the response runs the action again on retry. A database-backed store with the transactional guarantee is parked as a possible `store` sub-option.

## Request validation

**What.** FluentValidation validators check the *shape* of requests before the handler runs.

**Built (phase 6).** `Api/Configuration/Validation/`; documented for users in `docs/request-validation.md`.

**Design.**

- Request validation checks what the domain can't: missing or null fields, values that can't be parsed, requests that don't map to an entity. **The domain stays the authority** for business rules.
- **Limits and messages come from the domain** (`Widget.NameMaxLength`, the field errors in `Core/Domain/<Area>/<Entity>Errors.cs`), so a request rejected by its validator gets exactly the domain's response. Core's field errors are internal: the option adds `InternalsVisibleTo` for the Api to Core's project (an XML conditional) instead of making them public. Validators import them with an alias (`using WidgetFieldErrors = …WidgetErrors;`), since the Api has its own `WidgetErrors`. `Cascade(CascadeMode.Stop)` reports the first problem per field, like the domain.
- FluentValidation only (Apache 2.0). A validator class sits next to the request in the slice file, found by scanning (`AddValidatorsFromAssembly`).
- **Runs automatically, with no edit to `Register`:** `EndpointRegister` registers every endpoint on a group with an endpoint filter factory. The factory looks at the handler's parameters once; an endpoint whose arguments have no validator gets no filter. Features only *add* a validator class (rule 4).
- Always at the endpoint, even with CQRS: one location, and the domain still checks every business rule wherever a command comes from.
- Responses: status 400, title `Validation failed`, `errors` per field with JSON names, through the same `ProblemDetailsCustomization` (rule 7).
- Order on an endpoint: validation (a group filter) runs before the endpoint's own filters, so before idempotency.

**Feature template.** `CreateWidget` and `UpdateWidgetName` validate `Name`: required, at most `Widget.NameMaxLength`, with the domain's messages.

**Samples.** A validator for every sample request (user name and email, including the email's format through the domain's `EmailValidator`; task title and description).

**Tests.** The filter is tested directly (`Configuration/RequestValidationTests.cs`): an invalid argument gets the validation response and the handler doesn't run, a valid one reaches it, an endpoint without validated arguments gets no filter. Every existing invalid-field test passes unchanged with the option, now answered by the validator. A sample test shows validation runs first: a too-long name with an email in use gets 400, not 409.

**Trade-offs to document.** For simple CRUD on one entity it adds little; its value grows with requests that don't match an entity. MediatR users may expect a `ValidationBehavior`; this template doesn't use one, on purpose.
