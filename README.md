# CleanSlices

A starter .NET 10 Web API that mixes Clean Architecture layering with vertical slices, in a **pragmatic** way. It is a simple CRUD over two entities, **Tasks** and **Users**, and it is meant as a starting point for other projects.

"Pragmatic" means the layers separate what matters, the domain, but the code skips ceremony that doesn't pay off at this size:

- The API references EF Core and uses `AppDbContext` directly. There are no repositories and no unit-of-work abstraction.
- There is no MediatR or CQRS pipeline. Each endpoint holds its own handler logic.
- Comments are kept to a minimum. Names should explain the code.

## Command-line tool

A companion CLI creates solutions from this template, adds features to them, and merges optional patterns such as Result&lt;T&gt; into existing code. See **[docs/CLI.md](docs/CLI.md)** for installation and usage.

## Tech stack

- .NET 10 / C# 14 (extension blocks)
- ASP.NET Core Minimal APIs, ProblemDetails, OpenAPI
- EF Core 10 + SQL Server
- .NET Aspire 13 (AppHost + ServiceDefaults: OpenTelemetry, health checks, resilience)
- Central Package Management

## Solution layout

```
CleanSlices.slnx
Directory.Build.props        shared build settings (net10.0, nullable, warnings as errors, code style)
Directory.Packages.props     central package versions + transitive security pins
global.json                  opts `dotnet test` in to Microsoft.Testing.Platform
aspire/
  CleanSlices.AppHost        Aspire orchestrator (runs the API, dashboard)
  CleanSlices.ServiceDefaults OpenTelemetry, health checks, service discovery, resilience
src/
  CleanSlices.Core           domain: entities, domain events, domain errors, domain exceptions
  CleanSlices.Infrastructure EF Core, migrations, event publisher, event handlers
  CleanSlices.Api            endpoints (vertical slices), error mapping, exception handler
tests/
  CleanSlices.Core.Tests     unit tests: entities, validators
  CleanSlices.Api.Tests      integration tests: HTTP calls against the API and a real SQL Server
```

Dependencies point inwards: `Api → Infrastructure → Core`. Core has no package references.

## Architecture

### Core (domain)

- **`Entity`** is the abstract base class. It provides:
  - an `Id` (a version-7 GUID, so keys are sortable by time)
  - `CreatedAt`
  - the domain-event list (`Raise`, `DomainEvents`, `ClearDomainEvents`)

  It is abstract on purpose. It is never created on its own and is only a base for real entities.
- **`TaskItem`** and **`User`** are the entities. They:
  - create instances through `Create(...)` factories that validate the input and throw a `DomainValidationException` when it's invalid
  - change state only through methods (`UpdateTitle`, `Complete`, `AssignUser`, `Delete`, ...); setters are private
  - raise domain events (`Created`, `Completed`, `Deleted`)
- **`TaskItemUserAssignment`** is the join between tasks and users. It is *not* an `Entity`: it has a composite key `(TaskItemId, UserId)` plus `AssignedOn`. `AssignUser` and `UnassignUser` are all-or-nothing. Every id is validated before the collection changes.
- **Field errors** (`Core/Domain/*/…Errors.cs`) are `FieldError(Field, Description)` records. Each one describes one invalid value, such as an empty title or a name that is too long. `Field` is the property name (`nameof(User.Email)`).
- **Expected failures are exceptions** (`Core/Shared/Exceptions.cs`). All of them derive from `DomainException`, which carries a `Code`:
  - `DomainValidationException` (wraps a list of `FieldError`s; `DomainValidationException.ThrowIfAny(...)` throws only when a check failed)
  - `BusinessRuleException`
  - `NotFoundException`
  - `ConflictException`

  Prefer returning results over throwing? The `result` pattern replaces these with `Result<T>`. See [docs/CLI.md](docs/CLI.md).

### Api (vertical slices)

Every use case is one file under `Features/<Area>/`. The file holds:
- the request and response records
- the `Handler`
- a `Register` method that maps the route

Classes that implement `IEndpoint` are found by reflection and registered through `app.RegisterEndpoints()`.

Conventions:

- **API-level errors live next to the features**, not in Core. Examples are `Features/Users/UserErrors.cs` (`NotFound`, `EmailInUse`) and `Features/Tasks/TaskItemErrors.cs` (`NotFound`, `UserNotFound`). "Not found" and "email already used" depend on the database, so they are not domain rules.
- **Reads** use `AsNoTracking()` and an inline `Select(...)` into the response record. EF then reads only the columns it needs. Mapping helpers such as `.From(entity)` are avoided because they load the whole entity first.
- **Lists are paged.** A list handler binds `[AsParameters] PaginationParameters paging` (`?pageNumber=2&pageSize=10`), orders the query, reads one page with `.PaginateAsync(paging, cancellationToken)` and returns `page.ToResponse()`:

  ```json
  { "items": [ … ], "pageNumber": 2, "pageSize": 10, "totalCount": 57, "totalPages": 6, "hasPreviousPage": true, "hasNextPage": true }
  ```

  - The paging itself is in Infrastructure (`Pagination/`), next to EF: `PaginationParameters`, `PaginationList<T>` (one page of items plus its position) and `PaginateAsync`. It runs a `COUNT` for the totals, then `Skip`/`Take`.
  - `PaginationList<T>` is a read-only collection (`ReadOnlyCollection<T>`), built only by `PaginateAsync`. JSON writes collections as bare arrays, so the page information would be lost. The Api maps it to `PaginationResponse<T>` (`Configuration/PaginationResponse.cs`) with `ToResponse()`.
  - The defaults are page 1 and 10 items; `pageSize` is at most 50. Out-of-range values are clamped, never rejected: `pageNumber=0` becomes 1, `pageSize=1000` becomes 50.
  - Lists are ordered newest first (`CreatedAt`, then `Id`), so pages don't overlap or skip rows. A list must always be ordered before paging.
- **Lookups for updates** use `FindAsync([id], ct)`. Use `Include(...)` when a collection is needed, as in assign/unassign.
- **Handlers are plain imperative code**: load the entity, call the domain method, save, return `Results.*`. Failures are thrown (`throw UserErrors.NotFound(id)`) and never caught in the endpoint.
- **Exceptions become HTTP responses** in `Handlers/ExceptionHandler.cs`, which writes ProblemDetails through `IProblemDetailsService`:

  | Exception                   | Status |
  |-----------------------------|--------|
  | `DomainValidationException` | 400, with `errors` per field (see below) |
  | `BusinessRuleException`     | 400    |
  | `NotFoundException`         | 404    |
  | `ConflictException`         | 409    |
  | `BadHttpRequestException`   | 400    |
  | anything else               | 500    |

  Domain exceptions are expected, so they're logged at Information level. Anything else is logged as an error and returns a generic 500. Clients aren't told more than that: the log has the details.

  Validation failures use the standard `ValidationProblemDetails` shape: `errors` maps each field, named as in the JSON body, to its messages.

  ```json
  "errors": {
    "email": [ "Email is not a valid email address." ],
    "name": [ "Name cannot exceed 200 characters." ]
  }
  ```
- Every ProblemDetails response includes `instance` (`METHOD /path`), `requestId` and `traceId`.
- Enums are serialized as strings (`"Status": "InProgress"`).

### Infrastructure

- **`AppDbContext`** has two `DbSet`s, `Tasks` and `Users`, which also give the table names.
- **Type configurations** hold only what EF can't work out by convention:
  - maximum lengths, which reuse the domain constants (`TaskItem.TitleMaxLength`, ...)
  - a unique index on `User.Email`
  - the composite key and the `UserId` index on assignments

  EF already finds backing fields and foreign keys on its own, so there is no `ToTable`, `Navigation` or `HasMany` for those.
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
- **Event handlers** implement `INotificationHandler<T>`. They are found by scanning the assembly and registered with `TryAddEnumerable`, so one event can have several handlers:
  - logging handlers for the created, completed and deleted events
  - `UnassignDeletedUserFromTasksHandler`, which removes a deleted user from every task it was assigned to, in the same save as the delete
- **`EnrichSqlServerDbContext`** (from Aspire) adds connection retries, a health check, tracing and metrics to the DbContext.

## Endpoints

| Method | Route                             | Description                     |
|--------|-----------------------------------|---------------------------------|
| GET    | `/tasks?pageNumber=&pageSize=`    | List tasks, newest first, paged |
| GET    | `/tasks/{id}`                     | Get a task (includes `UserIds`) |
| POST   | `/tasks`                          | Create `{ title, description }` |
| PATCH  | `/tasks/{id}/title`               | `{ title }`                     |
| PATCH  | `/tasks/{id}/description`         | `{ description }`               |
| PATCH  | `/tasks/{id}/not-started`         | Set status                      |
| PATCH  | `/tasks/{id}/in-progress`         | Set status                      |
| PATCH  | `/tasks/{id}/completed`           | Set status                      |
| POST   | `/tasks/{id}/users/{userId}`      | Assign a user                   |
| DELETE | `/tasks/{id}/users/{userId}`      | Unassign a user                 |
| DELETE | `/tasks/{id}`                     | Delete a task                   |
| GET    | `/users?pageNumber=&pageSize=`    | List users, newest first, paged |
| GET    | `/users/{id}`                     | Get a user                      |
| POST   | `/users`                          | Create `{ name, email }` (409 if the email is used) |
| PATCH  | `/users/{id}/name`                | `{ name }`                      |
| PATCH  | `/users/{id}/email`               | `{ email }` (409 if the email is used) |
| DELETE | `/users/{id}`                     | Delete a user (also unassigns them from their tasks) |

`src/CleanSlices.Api/CleanSlices.Api.http` has ready-made requests. In Development the OpenAPI document is served at `/openapi/v1.json`.

## Getting started

### Prerequisites

- .NET 10 SDK
- A SQL Server instance. The default connection string uses `localhost` with Windows authentication.
- The EF Core CLI: `dotnet tool install -g dotnet-ef`, or `dotnet tool update -g dotnet-ef` to upgrade it
- Docker (Docker Desktop or Podman), only for the integration tests

### Database connection

The connection string is called `Database` and lives in the API's `appsettings.json`:

```json
"ConnectionStrings": {
  "Database": "Server=localhost;Database=CleanSlicesDb;Trusted_Connection=True;TrustServerCertificate=True"
}
```

The Aspire AppHost does **not** set up a database. The API always takes the connection string from its own configuration.

### Migrations

Migrations are **never applied on startup**. You create and apply them yourself with the EF CLI.

`dotnet ef` builds the DbContext through `AppDbContextFactory`. That factory reads the `ConnectionStrings__Database` environment variable, **not** appsettings. Set the variable to the same connection string the API uses:

```powershell
$env:ConnectionStrings__Database = "Server=localhost;Database=CleanSlicesDb;Trusted_Connection=True;TrustServerCertificate=True"

dotnet ef migrations add <Name> --project src/CleanSlices.Infrastructure
dotnet ef database update --project src/CleanSlices.Infrastructure
```

`dotnet ef` writes migrations to `src/CleanSlices.Infrastructure/Migrations`, EF's default folder. A new solution has none: the template doesn't ship migrations, because they depend on the database provider, so the first `migrations add` creates the folder. `.editorconfig` marks them as generated code, so the code-style rules don't apply to them.

### Run

With Aspire (dashboard, traces, metrics, logs):

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
- **`CleanSlices.Api.Tests`** call the API over HTTP. The API runs in memory (`WebApplicationFactory`) against a real SQL Server that Testcontainers starts in Docker, once for the whole run. The schema is created from the model with `EnsureCreated()`, so the tests don't depend on migrations. Each test creates its own data, so the database is never reset.

The tests use xUnit v3 on Microsoft.Testing.Platform, which `global.json` turns on for `dotnet test`. `cleanslices add feature` also generates tests for the new slice in both projects.

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
- **Concurrent duplicates** (two identical assigns, or the same email created twice at once) get past the pre-checks. The database constraint stops them, which results in a logged 500.
- **There is no optimistic concurrency** (no rowversion).
- **Expected failures are exceptions.** The code reads top to bottom, but method signatures don't show what can fail, and throwing costs more than returning. The `result` pattern is the alternative.
- **ProblemDetails titles are error codes**, such as `User.NotFound`.
- **Time comes from `DateTimeOffset.UtcNow`** rather than `TimeProvider`.
- **Any status change is allowed**, including `Completed → NotStarted`.

## Pending

- Add a migration that matches the current model:
  - drop the `Version` columns
  - remove `Id` and `CreatedAt` from `TaskItemUserAssignments`
  - make the composite primary key
- Optionally remove `AppDbContextFactory` and run EF with `--startup-project src/CleanSlices.Api`, so the connection string comes only from appsettings.
