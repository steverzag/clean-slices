# Base template backlog

Changes to `main` that the options depend on. They make the default solution better on their own, and they give the options the seams they need (rules 1 and 2 in [ROADMAP.md](../../ROADMAP.md#design-rules)).

## Persistence

### Ordered save pipeline

**Done** (`Persistence/SaveChanges/`). `SaveChangesPipeline`, the only interceptor, runs every `ISaveChangesStep`, sorted by `Order`. Steps are found by scanning, so an option adds a step file and never edits the pipeline.

A step can act at three moments:

- **Before the save**: change tracked entities, add rows. Everything is saved together.
- **After the commit**: things that must only happen once the data is saved.
- **When the save fails**: drop whatever the step prepared (realtime's buffer, for example).

| Order | Step | Phase | From |
|---|---|---|---|
| 0 | Domain events: dispatch, repeat until no new events | before | base (done) |
| 100 | Soft delete: a deleted `ISoftDeletable` becomes an update | before | soft delete strategy |
| 200 | *(reserved: optimistic concurrency, parked)* | before | – |
| 300 | Audit: write `AuditEntries` | before | audit strategy |
| – | Send buffered integration events (without outbox) | after | integration events |
| – | Send buffered realtime messages | after | realtime |

Order matters:

- **Domain events run first**, so the strategies also see what the handlers changed: audit records the handlers' changes, and soft delete converts a delete made by a handler. (The first design put them last, which would have missed both.)
- Soft delete runs before audit, or audit records a delete instead of the update it becomes.

### Domain events before the commit

**Done.** Domain events used to be published **after** the commit, and handlers saved again in a second transaction: if a handler failed, the first change was already saved.

- Domain events are dispatched **before the save**, and the handlers' changes are saved in the same save. Everything is saved together, or nothing is. A test proves it (`Deleting_a_user_saves_nothing_when_a_domain_event_handler_fails`).
- The step repeats until no new events are raised (at most 10 rounds), because a handler can raise more.
- Handlers change tracked entities and **never call `SaveChanges`**; the pipeline throws if one does. `UnassignDeletedUserFromTasksHandler` dropped its `SaveChangesAsync`.
- Rule: domain event handlers don't reach outside the app. Emails, other services and notifications that must survive a failure are integration events (see [patterns.md](patterns.md#integration-events-hidden-unit)).
- The README's trade-off "domain events run after the commit and outside the transaction" is gone.

### Model rules hook

`OnModelCreating` runs every model rule class found by scanning, after the type configurations. Strategies use it to apply query filters, extra columns and tables to entities with their marker interface, without editing `AppDbContext`.

### Bulk operations

Document: `ExecuteUpdate` and `ExecuteDelete` skip the save pipeline, so no strategy runs for them (no audit, and `ExecuteDelete` hard-deletes even with soft delete).

## Seams

### `ICurrentUser`

`string? UserId` and `bool IsAuthenticated`. The default implementation is anonymous (`null`). Authentication options provide one that reads the claims. Used by idempotency keys, audit, logging and realtime.

### `TimeProvider`

Replace `DateTimeOffset.UtcNow` with the injected `TimeProvider`. `Entity.CreatedAt` is set by a save step; domain methods that need the time receive it. Every strategy uses the same clock.

### `HybridCache`

`AddHybridCache()` plus the in-memory `IDistributedCache`. Idempotency keys store records in `IDistributedCache`; the Redis option replaces it and both pick Redis up. Verify that `HybridCache` doesn't use the in-memory `IDistributedCache` as a redundant second level.

## Features

### Pagination

List endpoints return pages. Offset or cursor is still to decide. The page type lives in `Core/Shared`, because repositories return it. Page and size are validated by the base template, not by the request validation option.

### Emails in lower case

SQL Server compares text without case by default; PostgreSQL doesn't. Storing emails in lower case in the domain keeps the unique index correct on both.

## Development experience

### Test projects

**Done.** Tests are part of every generated solution:

- xUnit v3 on Microsoft.Testing.Platform (`global.json`), plain `Assert`, no mocking library.
- `tests/CleanSlices.Core.Tests`: unit tests. `tests/CleanSlices.Api.Tests`: integration tests with `WebApplicationFactory` and a SQL Server container from Testcontainers, started once per run (assembly fixture), schema from `EnsureCreated()`, unique data per test instead of resets.
- Sample tests come with `--samples`. `add feature` generates `<Entity>Tests` and `<Entity>EndpointsTests`.
- When options arrive: the Api tests check responses, so the same tests should pass in every combination (rule 7). Only the Core tests need conditionals (for `result`). An authentication option will need a test login in the factory. PostgreSQL needs its own container in the factory.

### Aspire-managed database

The AppHost adds SQL Server (`AddSqlServer(...).AddDatabase("Database")`), so `dotnet run` on the AppHost starts it in a container and passes the connection string. The connection string in `appsettings.json` stays as a fallback for running the API alone. Needs Docker or Podman.

### Smaller items

- The README says migrations are in `Infrastructure/Persistence/Migrations`; the tracked ones are in `Infrastructure/Migrations`, the path `template.json` excludes.
- Browser clients on another domain (for example with realtime) need CORS with credentials. Decide whether the base template configures it.

## CLI

- Catalog and resolver: groups, `choices`, `requires` / `provides` / `conflicts`, hidden units, glue units, license notices.
- `.cleanslices.json` records options with their choices.
- Reject `--samples` with an invasive pattern or an authentication option.
- Retire pattern branches once `result` uses conditionals.
