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

**Built (phase 7b): RabbitMQ and Azure Service Bus.** Documented for users in `docs/messaging.md`.

**As built (Service Bus).**

- Official client (`Azure.Messaging.ServiceBus` 7.20, MIT) through Aspire (`AddAzureServiceBusClient("messaging")`). `ServiceBusDispatcher` sends to the topic with one sender; `ServiceBusConsumer` is a `ServiceBusProcessor` (no auto-complete): complete after the handlers, abandon on failure, dead-letter at once when unreadable. The event name is the message `Subject`; `traceparent` an application property.
- **The app creates no entities.** The emulator can't create them at run time, so the AppHost declares the topic and the subscription (`AddServiceBusTopic`, `AddServiceBusSubscription(..., subscriptionName)`, `MaxDeliveryCount = 5`); in Azure they come with the namespace. Their names follow the `Messaging` defaults through the template's name replacement; keeping them in step is documented. `MaxDeliveries` is RabbitMQ-only; Service Bus uses the subscription's `MaxDeliveryCount`.
- Aspire's emulator accepts Microsoft's emulator license by itself (`ACCEPT_EULA`, checked in its assembly); the tests accept it too (`WithAcceptLicenseAgreement`). The option prints a license notice: a paid service, an emulator for development only.
- **Tests**: Testcontainers' Service Bus emulator (2.0.1, with its SQL Server), configured by a JSON file `ApiFactory` writes from `BrokerOptions`' defaults. The broker tests became `BrokerTests` for both brokers, with a send-unreadable and a read-dead-letters helper per broker; all passed on the first run with the emulator.
- Not run: the AppHost with the emulator (Aspire's `RunAsEmulator`) was built, not started; the client side is covered by the tests against Testcontainers' emulator.

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

**Built (phase 7c): Keycloak, Entra ID, ASP.NET Core Identity.** Documented for users in `docs/authentication.md`.

**As built (Keycloak).**

- `Api/Configuration/Authentication/Keycloak/`: `KeycloakOptions` (`Authentication:Keycloak:` `Authority`, `Audience`; defaults in code match the AppHost), `AddKeycloakAuthentication()` (standard `AddJwtBearer`, `RequireHttpsMetadata` outside Development, `OnTokenValidated` turns `realm_access.roles` into role claims), `KeycloakOpenApi` (OAuth2 authorization code scheme, a document-wide requirement, added to the `v1` document through `OpenApiOptions`, so the `AddOpenApi()` line doesn't change).
- `Program.cs`: one registration line, and explicit `UseAuthentication` / `UseAuthorization` after `UseStatusCodePages`: the middleware ASP.NET Core adds by itself runs before them, so a 401 or 403 would have no ProblemDetails body.
- **AppHost**: a plain container (`quay.io/keycloak/keycloak:26.8`, `start-dev --import-realm`) on the fixed port **18080**, so the defaults in code, the `.http` file and tools know the URL; the master admin password is a generated, persisted parameter; health check on the management port. No data volume: the realm is imported on every start.
- **Realm** `aspire/…AppHost/Keycloak/realm.json` (a neutral file name: the template renames `cleanslices` inside files, not in this file's name): realm `<app>-realm`, public client `<app>-api` (PKCE, password grant for tests and the `.http` file, an audience mapper adding itself to `aud`), role `admin`, users `user` / `user` and `admin` / `admin` (first and last name and email set: Keycloak refuses logins of users whose profile is incomplete), one-hour access tokens.
- **Tests**: Testcontainers Keycloak with the same realm file (linked into the test project); `ApiFactory` logs `user` in once and adds the token in `ConfigureClient`, which derived factories reuse, so no existing test changed. `AuthenticationTests`: anonymous health and OpenAPI, 401 as ProblemDetails (samples), a bad token, `ICurrentUser.UserId` equals the token's `sub`, admin 200 / user 403 on a role check (in a small test app; sabotage-proved). The feature template gets a 401 test.
- Verified with the AppHost of a generated solution: the realm imports, the test users get tokens, no token or a bad one gives 401, a valid one reaches the handler.
- **Registration** (added after 7c): `registrationAllowed` and `registrationEmailAsUsername` in the realm, so the login page offers sign-up with an email (checked against a container: the login page links to the registration form, which asks for email and password only; the test users still log in by username). Google, Microsoft and other providers are realm identity providers that need the user's own credentials, so they are documented (`docs/authentication.md`, with a vault reference for the secret), not shipped. Several ways to sign in is the provider's job (federation); the API keeps one issuer.

**As built (Entra ID).**

- **The standard `AddJwtBearer`, not `Microsoft.Identity.Web`** (changed while building): for an API that only validates tokens for one tenant, Identity.Web adds MSAL, token acquisition and caches, and old `DataProtection` / `Cryptography.Xml` versions. What it would handle here is two Entra details, now two lines: `ValidIssuers` (v2 `…/{tenant}/v2.0` and v1 `https://sts.windows.net/{tenant}/`) and `ValidAudiences` (the client ID and `api://{client-id}`). The docs point to Identity.Web for downstream calls and multi-tenant apps. The JwtBearer package is shared with Keycloak (`keycloak || entra` package conditionals).
- `Api/Configuration/Authentication/Entra/`: `EntraOptions` (`Authentication:Entra:` `TenantId`, `ClientId` required, `Instance`, `Scope` `access_as_user`), `AddEntraAuthentication()` (fails at startup naming the missing settings; `OnTokenValidated` replaces the name identifier with `oid`: Entra's `sub` is pairwise per app), `EntraOpenApi` (OAuth2 authorization code against the tenant, scope `api://{client-id}/access_as_user`). App roles arrive in `roles`, which the default claim mapping already turns into role claims.
- **AppHost**: two parameters (`<app>-entra-tenant-id`, `<app>-entra-client-id`) passed to the API as environment variables; the dashboard asks for them on the first run. Not run: it needs a tenant.
- **Tests**: no tenant. `ApiFactory` signs its own tokens (HMAC test key, the option's v2 issuer and client ID, `oid` different from `sub`, `roles` for the admin) and replaces only the metadata: `options.ConfigurationManager` becomes a `StaticConfigurationManager` in a post-configure (setting `options.Configuration` there is too late: JwtBearer's own post-configure has already created a manager that fetches Entra's metadata, and every token got 401). The same `AuthenticationTests` run; the user-ID test expects `oid` (sabotage-proved).
- `.http`: `@token`, pasted from `az account get-access-token` (the docs say how to authorize the Azure CLI on the API).

**As built (ASP.NET Core Identity).**

- `Infrastructure/Identity/`: `AppUser : IdentityUser` (string keys, which `ICurrentUser.UserId` already is), `AppRoles.Admin`, and the role seeded with `HasData` in an `IEntityTypeConfiguration<IdentityRole>` found by the existing scan, so it comes with the migration and `EnsureCreated`. `addsTables`.
- `AppDbContext`: the declaration chain grew to eight members (`identity` × repository × CQRS), `base.OnModelCreating` first under `identity`. The samples' `Users` DbSet hides `IdentityDbContext.Users` with `new` (Identity's stores use `Set<TUser>()`); in this app `Users` stays the domain users.
- `Api/Configuration/Authentication/Identity/`: `AddIdentityCore<AppUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddApiEndpoints()` with the **bearer scheme only** (`AddIdentityApiEndpoints` adds cookies an API doesn't use); `MapIdentityApi` under `/account`. **Identity's endpoints don't opt out of the fallback policy** (found by the tests: register and login got 401), and `AllowAnonymous` on the group would open `manage/*` too (it wins over `RequireAuthorization`), so a convention opens every account endpoint except `manage/*`; a test checks both sides (sabotage-proved). OpenAPI: an HTTP bearer scheme.
- PostgreSQL: the existing `SnakeCaseTableNames` rule renames Identity's tables (`asp_net_users`, …); verified in a generated solution's schema script.
- **Tests**: the test users register and log in through the real endpoints (serialized; a second register gets 400), the admin gets the role through `UserManager`; every test host and the probe app share Data Protection keys (a test folder), since tokens are Data Protection payloads. The probe app turns off `ValidateOnBuild`: it reads tokens only and has no `AppDbContext`.
- Docs: Data Protection keys must be shared between instances in production; emails aren't sent until an `IEmailSender<AppUser>` is registered.


| Option | How | Local development |
|---|---|---|
| Keycloak | The API validates JWTs (the standard `AddJwtBearer`: Aspire's Keycloak packages are preview-only) | The AppHost runs Keycloak as a plain container and imports a realm with a client and two test users |
| Entra ID | The API validates JWTs (`Microsoft.Identity.Web`) | A tenant is needed; its IDs are settings to fill in |
| ASP.NET Core Identity | The API stores users; `MapIdentityApi` adds register and login endpoints | Works immediately; `AppDbContext` inherits `IdentityDbContext` (a conditional) |

**Design.**

- **The `authentication` hidden unit** holds what the three options share, like `message-broker` for the queues: the claims-based `ICurrentUser`, the fallback policy, and `.AllowAnonymous()` on health checks and the OpenAPI document. Each option requires it and brings only its own scheme. Its symbol is what code uses for "any authentication option".
- **The `login` capability** is provided by each option, not by the hidden unit, so a later option that needs logged-in users (permissions) requires `login`: the resolver then asks which provider to add instead of adding the hidden unit alone.
- The `authentication` unit provides `ICurrentUser` from the claims, replacing the anonymous default of the [`current-user`](hidden-units.md#current-user) hidden unit. The user ID is the standard name identifier claim, which each option fills.
- Secure by default: a fallback policy requires an authenticated user on every endpoint. Health checks and the OpenAPI document opt out with `.AllowAnonymous()`.
- **Roles work the same with every provider.** Each option turns its provider's roles into standard role claims, so `RequireRole("admin")` works unchanged whichever is selected. Keycloak puts realm roles in a nested `realm_access.roles` claim, flattened when the token is validated; Entra ID sends a `roles` claim (app roles); Identity has role claims once roles are enabled. A test per option: a user with the role gets in, one without gets 403. Permissions are a [parked option](../../ROADMAP.md#parked-and-open) built on this.
- Settings have their defaults in code, so `appsettings.json` stays untouched (it already has a PostgreSQL whole-file variant; auth variants would multiply them).
- Each option describes its login fully in the OpenAPI document (OAuth2 flows for Keycloak and Entra ID, bearer for Identity), so the API reference UI can log in with no glue.
- **The samples with authentication (decided in phase 7):** the sample `User` stays a domain user, not a login account (it only exists with `--samples`, and with Identity it would have to inherit `IdentityUser`). The sample endpoints are protected like any other. Linking a `User` to its login (a `LoginId` matched with `ICurrentUser.UserId`) is left for later.

**Feature template.** No change; the fallback policy protects new endpoints.

**Samples.** No code change: the sample endpoints are protected, and the tests log in with the option's test user (the test factory adds the token to every client).

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
