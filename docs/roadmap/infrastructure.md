# Infrastructure

An infrastructure option picks *which product* sits behind a seam. Options are organized in groups, and at most one option per group can be selected. Every group has a default that works with nothing selected. See the [catalog index](README.md).

**Every infrastructure option also sets up its Aspire resource** in the AppHost, so `dotnet run` on the AppHost starts what the solution needs in containers and passes the connection strings. Running the containers needs Docker or Podman.

## Database

**Options.** SQL Server (default) and PostgreSQL, in the required group `database`. SQLite is left out: EF Core can't sort or compare `DateTimeOffset` on SQLite, and its schema changes are limited. Tests use the real provider instead.

**Built (phase 5).** Documented for users in `docs/database.md`, which has a whole-file PostgreSQL variant.

**Design.** SQL Server is the plain side of every conditional: `#if (postgresql)` *Npgsql* `#else` *SQL Server* `#endif`. The `sqlserver` symbol exists (every unit has one) but no code uses it.

| Place | SQL Server | PostgreSQL |
|---|---|---|
| `InfrastructureRegistration` | `UseSqlServer` + `EnrichSqlServerDbContext` | `UseNpgsql` + `UseSnakeCaseNamingConvention` + `EnrichNpgsqlDbContext` |
| `AppDbContextFactory` | `UseSqlServer`, Windows-auth fallback | `UseNpgsql` + snake_case, `postgres`/`postgres` fallback |
| Packages (`Directory.Packages.props`, 3 `.csproj`, XML conditionals) | EF Core SQL Server, Aspire SQL Server (client, hosting), Testcontainers.MsSql | Npgsql EF Core, EFCore.NamingConventions, Aspire Npgsql (client, hosting), Testcontainers.PostgreSql, and a pin of `Microsoft.EntityFrameworkCore.Relational` |
| AppHost | `AddSqlServer("…-sql", port: 14330)` | `AddPostgres("…-postgres", port: 15432)`, database `cleanslices` |
| `appsettings.json` | local SQL Server | whole-file variant `appsettings.postgresql.json`, renamed in (JSON conditionals don't work: the template engine can't uncomment, and the repo's JSON would be invalid) |
| API tests | MsSql container | PostgreSQL container, Npgsql connection string builder |

- The database is called `Database` with both, so the API's connection string name doesn't change.
- **snake_case everywhere.** `UseSnakeCaseNamingConvention` renames columns, keys, indexes and conventional table names, but keeps names set with `ToTable(...)`. The model rule `SnakeCaseTableNames` renames those too, so type configurations stay provider-neutral (`ToTable("OutboxMessages")` becomes `outbox_messages`) and features added later are covered.
- **Case-insensitive email** (rule 7: same behaviour as SQL Server). The model rule `CaseInsensitiveCollation` defines an ICU collation that ignores case but not accents; the sample rule `UserEmailCollationRule` gives it to `User.Email`. The shared type configuration doesn't change. The test `Create_with_an_email_in_use_ignoring_case_returns_409` passes on both providers.
- PostgreSQL requires the [model rules](hidden-units.md#model-rules) hidden unit, built for it in phase 5.
- **The Relational pin.** Npgsql depends on an older `Microsoft.EntityFrameworkCore.Relational` than the EF Core version the template pins, and a PostgreSQL-only solution failed at startup. The repository build didn't show it: it references both providers, and the SQL Server package pulled the newer version in. Package resolution is only tested in generated solutions.
- Type configurations stay provider-neutral (rule 8): no `HasColumnType`. The outbox claim and `ExecuteUpdate` / `ExecuteDelete` translate on both.

**Feature template.** No change. A feature's `ToTable("Products")` becomes `products` on PostgreSQL through the model rule.

**Samples.** With PostgreSQL, `User.Email` is case-insensitive through `UserEmailCollationRule`.

**Replacing SQL Server** (`cleanslices add postgresql` on an existing solution): the code is merged like any option, and the CLI reminds the user to recreate migrations. Data isn't moved; `docs/database.md` says so and points to tools such as pgloader.

**Trade-offs to document** (in `docs/database.md`):

- Case sensitivity: only what a rule gives the collation ignores case. The docs show a rule for the user's own columns.
- PostgreSQL can't use `LIKE` (`Contains`, `StartsWith`) on a column with a nondeterministic collation.
- Npgsql stores `DateTimeOffset` as `timestamptz` and accepts only UTC. Times are always UTC.
- No pgAdmin by default; `docs/database.md` shows `.WithPgAdmin()`.

## Queue

**Options.** RabbitMQ and Azure Service Bus. The default is in-process.

**Built (phase 7b): RabbitMQ.** Service Bus is next. Documented for users in `docs/messaging.md`.

**As built (RabbitMQ).**

- **The dispatcher was split:** `IntegrationEventHandlers` runs every handler of a message (scopes, filters, the inbox); `IMessageDispatcher` only delivers. In process, the dispatcher calls the runner; with a broker, it sends, and the broker's consumer calls the runner. No behaviour change without a broker; the existing tests didn't change.
- **The [`message-broker`](hidden-units.md#message-broker) hidden unit** holds what the queue options share: `BrokerOptions` (`Messaging:` `Topic`, `Subscription`, `MaxDeliveries` 5, `Prefetch` 10; names default to `<solution>-events` / `<solution>-api`), the message format, and **`AfterCommitPublisher`**: without the outbox, the event is sent through `IAfterCommit` once the save commits. Each queue option requires the unit; it requires `integration-events` and `after-commit`. With the outbox also selected, `after-commit` is present but unused: a documented exception to rule 10, accepted over splitting the units further.
- **Topology** (declared once by `RabbitMqTopology`, by whichever comes first, publishing or consuming, because RabbitMQ drops what's published before a queue is bound): a durable topic exchange; the app's **quorum queue**, bound once per known event name; `x-delivery-limit` = `MaxDeliveries` − 1 and a dead-letter exchange, so the broker counts deliveries and dead-letters by itself. Redeliveries are immediate (no delayed retry without a plugin); documented.
- **Sending** opens a channel with publisher confirmations per message and waits for the confirm; persistent messages; `mandatory: false` (an event nobody is bound to isn't an error).
- **Receiving**: `RabbitMqConsumer`, a hosted service; ack after all handlers succeed, nack + requeue on failure, reject (to the dead-letter queue) for a name or payload that can't be read. Consumer activity `receive <Name>` parented to the `traceparent` header.
- **Wire format**: body = JSON payload, `Type` = event name (and routing key), `MessageId` = the outbox row's ID or a new one (stable across redeliveries, so the inbox dedupes), `traceparent` header.
- **Tests** run against a real broker (Testcontainers RabbitMQ) with the consumer running: tests send, then wait for a recording handler with a deadline. Extra test hosts (`WithEmptyDatabaseAsync`, `AnotherInstance()`) don't run the consumer, so they never take a message from the main host. Tests: delivery through the broker; publishing only after the commit (no outbox); a failing handler gets every delivery, then the dead-letter queue (with the inbox, the other handler ran once); an unreadable message is dead-lettered at once. Outbox tests wait for deliveries; the outbox's "failing handler" test runs only in process.
- **Matrix**: `rabbitmq+inbox` (inbox without outbox) is a listed combination on every push: the resolver alone would give the inbox the outbox, its first provider.

**Design.**

- Only **integration events** go through the queue. Domain events always run in-process, before the commit. A queue option requires the [integration events](hidden-units.md#integration-events) unit, which the resolver adds.
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

**Samples.** No code change: the three sample integration events go through the broker, which the AppHost runs. Their outbox-based tests wait for the handlers (the inbox record appears after the row is processed).

**Trade-offs to document.** Without the outbox, an event is lost if the broker is down at the moment of sending. Integration events are public contracts: add fields, never rename or remove them.

## Cache

**Options.** Two groups, both optional:

| Group | Option | What |
|---|---|---|
| `cache` | Redis | The shared store: `IDistributedCache` and `HybridCache`'s second level |
| `cache-library` | FusionCache | What runs `HybridCache`: FusionCache (MIT) instead of Microsoft's |

Plus the **`fusioncache-redis` glue unit**, added when both are selected: Redis as FusionCache's second level and its backplane. The first glue unit in the catalog.

**Built (phase 7a).** Documented for users in `docs/caching.md`.

**Design.**

- **The seam is `HybridCache`**, from the [`hybrid-cache`](hidden-units.md#hybridcache) hidden unit, which both options require. FusionCache is registered as `HybridCache` (`AsHybridCache()`), so features and samples are the same code with either library. FusionCache was added in phase 7 at the user's request; its extras (fail-safe, timeouts, eager refresh) are reached through `IFusionCache` when wanted.
- The Redis option replaces the in-memory `IDistributedCache` of the [`cache`](hidden-units.md#cache) unit, so idempotency keys and Microsoft's `HybridCache` pick it up with no glue. FusionCache needs glue: it only uses a second level and a backplane when told to.
- Removing cached copies goes through the [`after-commit`](hidden-units.md#after-commit) unit, required by `hybrid-cache`.
- Aspire: `AddRedis("cache")` in the AppHost; the client is the Aspire StackExchange.Redis distributed-cache integration (`AddRedisDistributedCache("cache")`). The FusionCache backplane uses the same connection string.
- Tests: a Redis container next to the database container (Testcontainers), only with the Redis option.
- **Redis prints a license notice** when selected (rule 9): source-available since 7.4, AGPLv3 added with Redis 8.

**Feature template.** Caches nothing. Caching is a per-endpoint decision; `docs/caching.md` shows caching a read, keys and tags, and removing from a domain event handler through `IAfterCommit`.

**Samples.** `GetUserById` reads through `HybridCache`, removed after updates and deletes. Only that read: the list would double another chain for little more to show (decided in phase 7). Assumes `UserUpdatedDomainEvent`, added to the sample domain for every solution.

**Matrix.** `fusioncache+redis` is a listed combination on every push (the glue). Listed combinations are now planned before pairs, so a listed one that is also a pair keeps its tier.

**Trade-offs documented.** Redis's license; Valkey and Garnet as open-source alternatives (only the AppHost line and its hosting package change). Without the backplane, other instances keep removed values until they expire. Removing after the commit narrows the stale window but can't close it. "Not found" is cached too. Per-user keys must include the user ID.

## Authentication

**Options.** Keycloak, Microsoft Entra ID, ASP.NET Core Identity. The default is none: an open API.

| Option | How | Local development |
|---|---|---|
| Keycloak | The API validates JWTs (`AddKeycloakJwtBearer`) | Aspire runs Keycloak and imports a realm with a client and a test user |
| Entra ID | The API validates JWTs (`Microsoft.Identity.Web`) | A tenant is needed; appsettings get placeholders |
| ASP.NET Core Identity | The API stores users; `MapIdentityApi` adds register and login endpoints | Works immediately; `AppDbContext` inherits `IdentityDbContext` (a conditional) |

**Design.**

- Each option provides `ICurrentUser` from the claims, replacing the anonymous default of the [`current-user`](hidden-units.md#current-user) hidden unit.
- Secure by default: a fallback policy requires an authenticated user on every endpoint. Health checks and the OpenAPI document opt out with `.AllowAnonymous()`. Roles and permissions are left to the user; the docs show a policy.
- Each option describes its login fully in the OpenAPI document (OAuth2 flows for Keycloak and Entra ID, bearer for Identity), so the API reference UI can log in with no glue.
- **Open (phase 7): the samples with authentication.** The sample `User` is a domain user, not a login account. Instead of rejecting `--samples`, design the link: the sample `User` as the profile of the authenticated user, matched by `ICurrentUser.UserId`.

**Feature template.** No change; the fallback policy protects new endpoints.

**Samples.** Open, see above. The sample endpoints are protected like any other, and the sample tests log in with the option's test user.

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

**Samples.** *Proposed:* the assignee is notified when a task is assigned (targeting a user needs an authentication option; without one, a group per task).

**Trade-offs to document.**

- Without authentication, only groups and "everyone" work. Clients can join a group like `user:42` themselves, which lets anyone claim to be anyone: demos only.
- The login ID (Keycloak or Entra subject) isn't the domain user's ID. Linking them is the app's decision; the usual approach is storing the login ID on the user entity.
- Notifications are best effort: users who aren't connected miss them. A stored list of notifications is a feature, not this option.
- Browser clients on another domain need CORS with credentials.
