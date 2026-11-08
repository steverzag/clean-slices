# CleanSlices

A starter .NET 10 Web API that mixes Clean Architecture layering with vertical slices, in a **pragmatic** way. It is meant as a starting point for other projects: add features to it, and options when you need them.

"Pragmatic" means the layers separate what matters, the domain, but the code skips ceremony that doesn't pay off at this size:

- The API references EF Core and uses `AppDbContext` directly. There are no repositories and no unit-of-work abstraction.
- There is no MediatR or CQRS pipeline. Each endpoint holds its own handler logic.
- Comments are kept to a minimum. Names should explain the code.

## Command-line tool

A companion CLI creates solutions from this template with the options you pick, adds features that follow those options, and merges more options such as Result&lt;T&gt; into existing code. See **[docs/CLI.md](docs/CLI.md)** for installation and usage.

## Options in this solution

The options the solution was created with, each with its own documentation:

<!--#if (samples)-->
- **Sample features**: Tasks and Users, a reference for your own features. See [docs/samples.md](docs/samples.md).
<!--#endif-->
<!--#if (result)-->
- **Result&lt;T&gt;**: expected failures are results instead of exceptions. See [docs/error-handling.md](docs/error-handling.md).
<!--#endif-->
<!--#if (outbox)-->
- **Outbox**: integration events are stored with the data and delivered in the background, with retries. See [docs/outbox.md](docs/outbox.md) and [docs/integration-events.md](docs/integration-events.md).
<!--#endif-->
<!--#if (inbox)-->
- **Inbox**: each integration event handler runs once per event, even when it's delivered again. See [docs/inbox.md](docs/inbox.md).
<!--#endif-->
<!--#if (postgresql)-->
- **PostgreSQL** instead of SQL Server, with snake_case names. See [docs/database.md](docs/database.md).
<!--#endif-->
<!--#if (validation)-->
- **Request validation**: FluentValidation validators check requests before the handler; the domain stays the authority. See [docs/request-validation.md](docs/request-validation.md).
<!--#endif-->
<!--#if (idempotency)-->
- **Idempotency keys**: a retry with the same `Idempotency-Key` header gets the stored response instead of running again. See [docs/idempotency.md](docs/idempotency.md).
<!--#endif-->
<!--#if (repository)-->
- **Repository / unit of work**: endpoints use a repository per entity and `IUnitOfWork`, defined in Core, instead of `AppDbContext`. See [docs/repository.md](docs/repository.md).
<!--#endif-->
<!--#if (cqrs)-->
- **CQRS**: endpoints send commands and queries to handlers in the slice; queries read without tracking. See [docs/cqrs.md](docs/cqrs.md).
<!--#endif-->
<!--#if (redis)-->
- **Redis** behind the cache, shared by every instance. See [docs/caching.md](docs/caching.md).
<!--#endif-->
<!--#if (fusioncache)-->
- **FusionCache** runs `HybridCache`: fail-safe and timeouts, and with Redis a backplane. See [docs/caching.md](docs/caching.md).
<!--#endif-->
<!--#if (rabbitmq)-->
- **RabbitMQ**: integration events go through a broker, retried and dead-lettered. See [docs/messaging.md](docs/messaging.md).
<!--#endif-->
<!--#if (servicebus)-->
- **Azure Service Bus**: integration events go through a topic (the emulator locally), retried and dead-lettered. See [docs/messaging.md](docs/messaging.md).
<!--#endif-->
<!--#if (keycloak)-->
- **Keycloak**: every endpoint requires a login; the API validates Keycloak tokens, and the AppHost runs Keycloak with test users. See [docs/authentication.md](docs/authentication.md).
<!--#endif-->
<!--#if (entra)-->
- **Microsoft Entra ID**: every endpoint requires a login; the API validates Entra ID tokens for your app registration. See [docs/authentication.md](docs/authentication.md).
<!--#endif-->
<!--#if (signalr)-->
- **SignalR**: messages pushed to connected clients (a user, a group, everyone), sent after the commit. See [docs/realtime.md](docs/realtime.md).
<!--#endif-->
<!--#if (soft_delete)-->
- **Soft delete**: deleting an entity sets `DeletedAt` instead of removing the row; deleted rows are hidden. See [docs/soft-delete.md](docs/soft-delete.md).
<!--#endif-->
<!--#if (identity)-->
- **ASP.NET Core Identity**: every endpoint requires a login; the API keeps its own accounts, with register and login endpoints under `/account`. See [docs/authentication.md](docs/authentication.md).
<!--#endif-->
<!--#if (!samples && !result && !outbox && !inbox && !postgresql && !validation && !idempotency && !repository && !cqrs && !redis && !fusioncache && !rabbitmq && !servicebus && !keycloak && !entra && !identity && !signalr && !soft_delete)-->
- None: this is the default setup. Options can be added later with the CLI.
<!--#endif-->

## Tech stack

- .NET 10 / C# 14 (extension blocks)
- ASP.NET Core Minimal APIs, ProblemDetails, OpenAPI
<!--#if (postgresql)-->
- EF Core 10 + PostgreSQL (Npgsql)
<!--#else-->
- EF Core 10 + SQL Server
<!--#endif-->
- .NET Aspire 13 (AppHost + ServiceDefaults: OpenTelemetry, health checks, resilience)
- Central Package Management

## Solution layout

```
CleanSlices.slnx
Directory.Build.props        shared build settings (net10.0, nullable, warnings as errors, code style)
Directory.Packages.props     central package versions + transitive security pins
global.json                  opts `dotnet test` in to Microsoft.Testing.Platform
aspire/
  CleanSlices.AppHost        Aspire orchestrator (runs the database, the API, dashboard)
  CleanSlices.ServiceDefaults OpenTelemetry, health checks, service discovery, resilience
src/
  CleanSlices.Core           domain: entities, domain events, domain errors
  CleanSlices.Infrastructure EF Core, migrations, event publisher, event handlers
  CleanSlices.Api            endpoints (vertical slices), error mapping
tests/
  CleanSlices.Core.Tests     unit tests: entities, validators
  CleanSlices.Api.Tests      integration tests: HTTP calls against the API and a real database
```

Dependencies point inwards: `Api → Infrastructure → Core`. Core has no package references.

## Architecture

### Core (domain)

- **`Entity`** is the abstract base class. It provides:
  - an `Id` (a version-7 GUID, so keys are sortable by time)
  - `CreatedAt`
  - the domain-event list (`Raise`, `DomainEvents`, `ClearDomainEvents`)

  It is abstract on purpose. It is never created on its own and is only a base for real entities.
- **Entities** (`Core/Domain/<Area>/`) derive from `Entity`. They:
  - create instances through `Create(...)` factories that validate the input
  - change state only through methods (`UpdateName`, `Delete`, ...); setters are private
  - raise domain events (`Created`, `Deleted`, ...)
- **Field errors** (`Core/Domain/<Area>/<Entity>Errors.cs`) describe one invalid value each, such as an empty name.
- **Expected failures** (invalid input, not found, conflicts) and how they become HTTP responses are described in [docs/error-handling.md](docs/error-handling.md).

### Api (vertical slices)

Every use case is one file under `Features/<Area>/`. The file holds:
- the request and response records
- the `Handler`
- a `Register` method that maps the route

Classes that implement `IEndpoint` are found by reflection and registered through `app.RegisterEndpoints()`.

Conventions:

- **API-level errors live next to the features**, not in Core (`Features/<Area>/<Entity>Errors.cs`). "Not found" and "email already used" depend on the database, so they are not domain rules.
- **Reads** use `AsNoTracking()` and an inline `Select(...)` into the response record. EF then reads only the columns it needs. Mapping helpers such as `.From(entity)` are avoided because they load the whole entity first.
- **Lists are paged.** A list handler binds `[AsParameters] PaginationParameters paging` (`?pageNumber=2&pageSize=10`), orders the query, reads one page with `.PaginateAsync(paging, cancellationToken)` and returns `page.ToResponse()`:

  ```json
  { "items": [ … ], "pageNumber": 2, "pageSize": 10, "totalCount": 57, "totalPages": 6, "hasPreviousPage": true, "hasNextPage": true }
  ```

  - The paging itself is in Infrastructure (`Pagination/`), next to EF: `PaginationParameters`, `PaginationList<T>` (one page of items plus its position) and `PaginateAsync`. It runs a `COUNT` for the totals, then `Skip`/`Take`.
  - `PaginationList<T>` is a read-only collection (`ReadOnlyCollection<T>`), built only by `PaginateAsync`. JSON writes collections as bare arrays, so the page information would be lost. The Api maps it to `PaginationResponse<T>` (`Configuration/PaginationResponse.cs`) with `ToResponse()`.
  - The defaults are page 1 and 10 items; `pageSize` is at most 50. Out-of-range values are clamped, never rejected: `pageNumber=0` becomes 1, `pageSize=1000` becomes 50.
  - Lists are ordered newest first (`CreatedAt`, then `Id`), so pages don't overlap or skip rows. A list must always be ordered before paging.
- **Lookups for updates** use `FindAsync([id], ct)`. Use `Include(...)` when a collection is needed.
- **Handlers** load the entity, call the domain method, save and return a response. How they report failures, and how failures become ProblemDetails responses, is in [docs/error-handling.md](docs/error-handling.md).
- Enums are serialized as strings (`"Status": "InProgress"`).

### Infrastructure

- **`AppDbContext`** has no `DbSet` per feature. Features reach their entities with `context.Set<T>()`, so adding one never edits `AppDbContext`.
- **Type configurations** (`Persistence/Configuration/`) are found by scanning the assembly and hold only what EF can't work out by convention:
  - the table name (`ToTable(...)`), since there's no `DbSet` to give it
  - maximum lengths, which reuse the domain constants (`Product.NameMaxLength`, ...)
  - unique indexes and composite keys

  EF already finds backing fields and foreign keys on its own, so there is no `Navigation` or `HasMany` for those.
- **The save pipeline** (`Persistence/SaveChanges/`) is the only interceptor on the DbContext. `SaveChangesPipeline` runs every `ISaveChangesStep` it finds in the assembly, sorted by `Order`. A step can act at three moments:
  - **before the save**: its changes are saved together with everything else
  - **after the commit**: for work that must only happen if the save succeeded
  - **when the save fails**: to drop whatever it prepared

  Adding a step means adding a class; the pipeline doesn't change. Only `SaveChangesAsync` is supported.
- **Domain events** are dispatched by `DomainEventsStep`, the first step, **before the save**, through the in-process `IPublisher`:
  - What the handlers change is saved **in the same save** as the change that raised the event. If a handler fails, **nothing is saved** and the client gets a 500.
  - Handlers can raise more events; those are dispatched in further rounds (at most 10).
  - **Handlers never call `SaveChanges`.** They only change tracked entities; the pipeline throws if a handler saves.
  - Handlers don't reach outside the app (emails, other services): a later failure would undo the data but not the email.
- **Event handlers** (`EventHandlers/<Area>/`) implement `INotificationHandler<T>`. They are found by scanning the assembly and registered with `TryAddEnumerable`, so one event can have several handlers. A new feature gets logging handlers for its created and deleted events.
<!--#if (postgresql)-->
- **`EnrichNpgsqlDbContext`** (from Aspire) adds connection retries, a health check, tracing and metrics to the DbContext.
<!--#else-->
- **`EnrichSqlServerDbContext`** (from Aspire) adds connection retries, a health check, tracing and metrics to the DbContext.
<!--#endif-->

## Endpoints

Each feature maps its own routes. A feature added with the CLI gets five, for example for `Product`:

| Method | Route                              | Description                        |
|--------|------------------------------------|------------------------------------|
| GET    | `/products?pageNumber=&pageSize=`  | List, newest first, paged          |
| GET    | `/products/{id}`                   | Get one                            |
| POST   | `/products`                        | Create `{ name }`                  |
| PATCH  | `/products/{id}/name`              | `{ name }`                         |
| DELETE | `/products/{id}`                   | Delete                             |

In Development the OpenAPI document is served at `/openapi/v1.json`, and `src/CleanSlices.Api/CleanSlices.Api.http` is the place for ready-made requests.

## Getting started

### Prerequisites

- .NET 10 SDK
- Docker (Docker Desktop or Podman): for the database when you run the AppHost, and for the integration tests. Not needed for the AppHost if you use an existing server (see [docs/database.md](docs/database.md)).
- The EF Core CLI: `dotnet tool install -g dotnet-ef`, or `dotnet tool update -g dotnet-ef` to upgrade it

### Database

The connection string is called `Database`. With the AppHost, the database runs in a container, or the AppHost uses an existing server you configure. Migrations are never applied on startup: you create and apply them with the EF CLI. Both are described in [docs/database.md](docs/database.md).

### Run

With Aspire (the database in a container, dashboard, traces, metrics, logs):

```
dotnet run --project aspire/CleanSlices.AppHost
```

Or run the API on its own:

```
dotnet run --project src/CleanSlices.Api
```

The API listens on `http://localhost:5221` and `https://localhost:7155`.

### Test

```
dotnet test
```

- **`CleanSlices.Core.Tests`** are unit tests for the entities and validators. They need nothing else.
- **`CleanSlices.Api.Tests`** call the API over HTTP. The API runs in memory (`WebApplicationFactory`) against a real database that Testcontainers starts in Docker, once for the whole run. The schema is created from the model with `EnsureCreated()`, so the tests don't depend on migrations. Each test creates its own data, so the database is never reset.

The tests use xUnit v3 on Microsoft.Testing.Platform, which `global.json` turns on for `dotnet test`. Adding a feature with the CLI also generates tests for the new slice in both projects.

## Build conventions

- `Directory.Build.props` sets the shared properties, so the `.csproj` files only list references:
  - target framework
  - `Nullable`
  - `ImplicitUsings`
  - `TreatWarningsAsErrors`
  - `EnforceCodeStyleInBuild`
- `Directory.Packages.props` holds every package version. `CentralPackageTransitivePinningEnabled` pins patched versions of vulnerable transitive packages (`System.Security.Cryptography.Pkcs`, `MessagePack`). NuGet audit warnings break the build, so a new advisory shows up immediately.
- Code uses file-scoped namespaces and tabs, as set in `.editorconfig`.

## Known trade-offs

These are deliberate for a simple starter. Revisit them when the project grows.

- **Deleting takes two steps.** The entity's `Delete()` raises the event, then `Remove()` deletes the row.
- **Concurrent duplicates** (the same unique value created twice at once) get past a handler's pre-check. The database constraint stops them, which results in a logged 500.
- **There is no optimistic concurrency** (no rowversion).
- **ProblemDetails titles are error codes**, such as `Product.NotFound`.
- **Time comes from `DateTimeOffset.UtcNow`** rather than `TimeProvider`.

The trade-offs of error handling are in [docs/error-handling.md](docs/error-handling.md).
