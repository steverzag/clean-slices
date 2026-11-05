# Infrastructure

An infrastructure option picks *which product* sits behind a seam. Options are organized in groups, and at most one option per group can be selected. Every group has a default that works with nothing selected. See the [catalog index](README.md).

**Every infrastructure option also sets up its Aspire resource** in the AppHost, so `dotnet run` on the AppHost starts what the solution needs in containers and passes the connection strings. Running the containers needs Docker or Podman.

## Database

**Options.** SQL Server (default) and PostgreSQL. SQLite is left out: EF Core can't sort or compare `DateTimeOffset` on SQLite, and its schema changes are limited. Tests use the real provider instead.

**Design.** The provider appears in four places, each a small conditional:

| Place | SQL Server | PostgreSQL |
|---|---|---|
| `InfrastructureRegistration` | `UseSqlServer` + `EnrichSqlServerDbContext` | `UseNpgsql` + `EnrichNpgsqlDbContext` |
| `AppDbContextFactory` | `UseSqlServer` and its fallback connection string | `UseNpgsql` and its fallback connection string |
| Packages | EF Core SQL Server, Aspire SQL Server | Npgsql EF Core, Aspire Npgsql |
| AppHost | `AddSqlServer(...).AddDatabase("Database")` | `AddPostgres(...).AddDatabase("Database")` |

- The database is called `Database` with both, so the API's connection string name doesn't change.
- PostgreSQL uses `snake_case` table and column names (`EFCore.NamingConventions`).
- Type configurations stay provider-neutral (rule 8): no `HasColumnType`.

**Feature template.** No change.

**Trade-offs to document.**

- Case sensitivity: SQL Server's default collation ignores case, PostgreSQL's doesn't. The base template stores emails in lower case; user code with text uniqueness must do the same.
- Npgsql stores `DateTimeOffset` as `timestamptz` and accepts only UTC. Times are always UTC.

## Queue

**Options.** RabbitMQ and Azure Service Bus. The default is in-process.

**Design.**

- Only **integration events** go through the queue. Domain events always run in-process, before the commit. A queue option requires the [integration events](patterns.md#integration-events-hidden-unit) unit, which the resolver adds.
- The option provides `IMessageDispatcher` (send) and a consumer that hands each message to the `IIntegrationEventHandler`s. It works with or without the outbox, with no glue.
- Official clients (`RabbitMQ.Client`, `Azure.Messaging.ServiceBus`) with their Aspire integrations. No messaging framework: MassTransit and NServiceBus have commercial licenses, and frameworks bring their own outbox, inbox and retries, which would duplicate ours.
- Topology: one topic-style exchange or Service Bus topic per app; the routing key is the event type name; the app's own queue or subscription is bound to it. Names default to the solution name and are configurable.
- Reliability:

  | Step | RabbitMQ | Service Bus |
  |---|---|---|
  | Sending | Publisher confirms, durable exchange, persistent messages | Built in |
  | Receiving | Ack after the handlers succeed | Complete after the handlers succeed |
  | Failure | Up to N redeliveries, then a dead-letter queue | `MaxDeliveryCount`, then the built-in dead-letter queue |

- With the outbox, a row is marked done only after the broker confirms.
- Aspire: `AddRabbitMQ("messaging")` with the management UI; `AddAzureServiceBus(...).RunAsEmulator()` for local development.
- The option provides `at-least-once-delivery`, which makes the [inbox](patterns.md#inbox) available.

**Feature template.** No change.

**Trade-offs to document.** Without the outbox, an event is lost if the broker is down at the moment of sending. Integration events are public contracts: add fields, never rename or remove them.

## Cache

**Options.** Redis. The default is in memory.

**Design.**

- The seam is `HybridCache` in the base template, plus the in-memory `IDistributedCache`.
- The Redis option registers a Redis `IDistributedCache`. `HybridCache` uses it as its second level, and idempotency keys switch to it too, with no glue.
- Aspire: `AddRedis("cache")`; the client is the Aspire StackExchange.Redis distributed-cache integration.

**Feature template.** Caches nothing. Caching is a per-endpoint decision. The docs show reading through `HybridCache` with tags (`widget:{id}`, `widgets`) and invalidating from domain event handlers (`WidgetUpdated`, `WidgetDeleted`), which run in the same request.

**Trade-offs to document.** Redis changed its license in 2024 and added an AGPL option in 2025. The container is fine for development; the production choice is the user's. Valkey and Garnet speak the same protocol, are open source and supported by Aspire; only the AppHost line changes. Cache keys for per-user data must include the user ID.

## Authentication

**Options.** Keycloak, Microsoft Entra ID, ASP.NET Core Identity. The default is none: an open API.

| Option | How | Local development |
|---|---|---|
| Keycloak | The API validates JWTs (`AddKeycloakJwtBearer`) | Aspire runs Keycloak and imports a realm with a client and a test user |
| Entra ID | The API validates JWTs (`Microsoft.Identity.Web`) | A tenant is needed; appsettings get placeholders |
| ASP.NET Core Identity | The API stores users; `MapIdentityApi` adds register and login endpoints | Works immediately; `AppDbContext` inherits `IdentityDbContext` (a conditional) |

**Design.**

- Each option provides `ICurrentUser` from the claims, replacing the anonymous default.
- Secure by default: a fallback policy requires an authenticated user on every endpoint. Health checks and the OpenAPI document opt out with `.AllowAnonymous()`. Roles and permissions are left to the user; the docs show a policy.
- Each option describes its login fully in the OpenAPI document (OAuth2 flows for Keycloak and Entra ID, bearer for Identity), so the API reference UI can log in with no glue.
- `--samples` is rejected with an authentication option: the sample `User` entity is a domain user, not a login account.

**Feature template.** No change; the fallback policy protects new endpoints.

**Interactions.** Idempotency keys become per user; audit and logging record the user; realtime targets users.

## Realtime

**Options.** SignalR. The default is none. Server-sent events may come later as a second option, for simple one-way streams.

**Design.**

- The option brings `IRealtimeNotifier`: `SendToUserAsync`, `SendToGroupAsync`, `SendToAllAsync`.
- Messages are never sent for changes that might not be saved: during a save (for example from a domain event handler) the notifier buffers them, and the save pipeline's after-commit phase sends them. If the save fails, they're dropped. Outside a save, they're sent immediately.
- No dependency on integration events.
- Sending to a user needs an authentication option: SignalR identifies users with the same claim as `ICurrentUser`. Browsers send the token in the query string for WebSockets; the option configures that for Keycloak and Entra ID (both JWT). ASP.NET Core Identity uses a different token handler, so that pair is a **glue unit**.
- Several instances: one instance by default. With Redis selected, SignalR's Redis backplane is added as a **glue unit** (realtime × Redis). Azure SignalR Service may come later as a sub-option.

**Feature template.** No change. The docs show a domain event handler notifying a user, for example "you were assigned to a task".

**Trade-offs to document.**

- Without authentication, only groups and "everyone" work. Clients can join a group like `user:42` themselves, which lets anyone claim to be anyone: demos only.
- The login ID (Keycloak or Entra subject) isn't the domain user's ID. Linking them is the app's decision; the usual approach is storing the login ID on the user entity.
- Notifications are best effort: users who aren't connected miss them. A stored list of notifications is a feature, not this option.
- Browser clients on another domain need CORS with credentials.
