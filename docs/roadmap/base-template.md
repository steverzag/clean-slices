# Base template backlog

Changes to `main`. Each one must make the default solution better on its own (rule 10 in [ROADMAP.md](../../ROADMAP.md#design-rules)). Seams that only options use are hidden units instead: see [Moved out of the base template](#moved-out-of-the-base-template).

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
- Rule: domain event handlers don't reach outside the app. Emails, other services and notifications that must survive a failure are integration events (see [hidden-units.md](hidden-units.md#integration-events)).
- The README's trade-off "domain events run after the commit and outside the transaction" is gone.

### Bulk operations

Document: `ExecuteUpdate` and `ExecuteDelete` skip the save pipeline, so no strategy runs for them (no audit, and `ExecuteDelete` hard-deletes even with soft delete).

## Features

### Pagination

**Done.** Offset paging, adapted from the pagination in `exp-demo-worker` (`Feature/Pagination`):

- **Infrastructure** (`Pagination/`), next to EF: `PaginationParameters` (`?pageNumber=&pageSize=`), `PaginationList<T>` and `PaginateAsync`. List endpoints bind `[AsParameters] PaginationParameters`.
- **Api** (`Configuration/PaginationResponse.cs`): `PaginationList<T>` is a collection, which JSON writes as a bare array, so endpoints return `page.ToResponse()`: `{ items, pageNumber, pageSize, totalCount, totalPages, hasPreviousPage, hasNextPage }`.
- Changes from the original: renamed `PaginatedList` to `PaginationList` and `PageIndex` to `PageNumber`; added `TotalCount` and `PageSize`; dropped the sync `Create`; made the constructor private and `CreateAsync` internal, so `PaginateAsync` is the only way to build a page; inherits `ReadOnlyCollection<T>` instead of `List<T>`, so a page can't be changed after it's read (its totals would no longer match); added a `CancellationToken`; clamped the lower end too (page ≥ 1, size ≥ 1), since 0 meant a 500 or a division by zero. The parameters come through the constructor: with settable `int` properties, `[AsParameters]` makes them required, so a missing value would be a 400.
- Defaults: page 1, 10 items; at most 50. Out-of-range values are clamped silently, so the same code works with and without `result`, and no validation is needed.
- Newest first: `CreatedAt` descending, then `Id`.
- The repository option can't return `PaginationList<T>` from a Core interface, since it lives in Infrastructure; that option adds a Core page type or moves it.
- Cursor paging isn't planned. If it's ever needed, it's a catalog option, and it can't sort by `Id` alone: SQL Server doesn't order version-7 GUIDs by time.
- Tests count rows in their own empty database (`ApiFactory.WithEmptyDatabaseAsync()`), because other tests add rows to the shared one at the same time.

## Development experience

### Test projects

**Done.** Tests are part of every generated solution:

- xUnit v3 on Microsoft.Testing.Platform (`global.json`), plain `Assert`, no mocking library.
- `tests/CleanSlices.Core.Tests`: unit tests. `tests/CleanSlices.Api.Tests`: integration tests with `WebApplicationFactory` and a SQL Server container from Testcontainers, started once per run (assembly fixture), schema from `EnsureCreated()`, unique data per test instead of resets.
- Sample tests come with `--samples`. `add feature` generates `<Entity>Tests` and `<Entity>EndpointsTests`.
- When options arrive: the Api tests check responses, so the same tests should pass in every combination (rule 7). Only the Core tests need conditionals (for `result`). An authentication option will need a test login in the factory. PostgreSQL needs its own container in the factory.

### Aspire-managed database

**Done** (`AppHost.cs`). `dotnet run` on the AppHost starts SQL Server in a container and passes the `Database` connection string to the API.

- **Fixed port** `14330`, so `dotnet ef` can reach the container with a known connection string. Use `127.0.0.1,14330`: Aspire's port proxy listens on `::1` too but only forwards on IPv4, and `localhost` makes the SQL Server client try IPv6 first and fail (found while testing).
- **Password:** Aspire generates the `sa` password on the first run and keeps it in the AppHost's user secrets (`Parameters:cleanslices-sql-password`). Each generated solution has its own user-secrets ID, so its own password. The README shows how to read it for `dotnet ef`.
- **Data volume:** the data survives restarts (tested: after a restart the migrated tables were still there).
- **Existing server:** if the AppHost has `ConnectionStrings:Database` (user secrets or appsettings), it uses that with `AddConnectionString` and starts no container (tested).
- Migrations are still never applied on startup; a new container starts empty.
- The API's `appsettings.json` keeps its local connection string for running the API without the AppHost.
- A database name with a dot (`Acme.Orders`, from the solution name) works: Aspire quotes it when creating the database (tested with a generated solution).
- Stopping the AppHost with Ctrl+C removes its container; killing the process leaves it running, to remove by hand.

### Smaller items

- **Done.** The README pointed to `Infrastructure/Persistence/Migrations`; migrations are in `Infrastructure/Migrations`, the path `template.json` excludes. The README now also says new solutions start without migrations.
- Browser clients on another domain (for example with realtime) need CORS with credentials. Decide whether the base template configures it.

## CLI

- **Done (phase 2).** Catalog and resolver: groups, `choices`, `requires` / `provides` / `conflicts`, hidden units, glue units, license notices.
- **Done (phase 2).** `.cleanslices.json` (format 2) records options with their choices and what automatic units were added for.
- **Done (phase 2).** Options declare whether the samples support them (`"samples"`, default true); `--samples` is rejected only with an option that declares `false`.
- **Done (phase 2).** Pattern branches retired: `result` uses conditionals.

## Moved out of the base template

These were planned for `main` and moved out: nothing in the default solution would use them, or their main benefit was testability. The default solution only contains what it uses itself (rule 10).

| Item | Now | Why |
|---|---|---|
| `ICurrentUser` | hidden unit [`current-user`](hidden-units.md#current-user) | Without authentication it always answers "anonymous", and only options read it |
| Model rules hook | hidden unit [`model-rules`](hidden-units.md#model-rules) | Only strategies use it |
| `HybridCache` | hidden unit [`hybrid-cache`](hidden-units.md#hybridcache) | Features cache nothing by default; only the caching options (Redis, FusionCache) bring it |
| Lower-case emails | the [PostgreSQL option](infrastructure.md#database) | SQL Server's default collation already ignores case: a second user with `ana@x.com` after `Ana@X.com` gets a 409 (tested). Only PostgreSQL has the problem |
| `TimeProvider` | possible option, concept to decide | Built, then reverted: its main benefit is testability, which shouldn't shape the default solution |

### `TimeProvider`

The default solution keeps `DateTimeOffset.UtcNow`. The reverted design, for when it's picked up: `Entity.CreatedAt` set by a save step (order 10, after domain events) from `TimeProvider`; domain methods receive the time as a value (`Complete(now)`, `AssignUser(userId, now)`); handlers pass `timeProvider.GetUtcNow()`; the API tests' factory fixes the clock with `FakeTimeProvider`. No analyzer: using it is a convention. Changing domain signatures makes it a pattern by our definitions.

Options that need the time (soft delete, audit, outbox, idempotency) read the clock the same way the rest of the solution does.
