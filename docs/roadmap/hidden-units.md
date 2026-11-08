# Hidden units

A hidden unit is a piece that several options need but nobody picks on its own. The resolver adds it when a selected option `requires` it, so it's never in a solution that doesn't use it (rule 10 in [ROADMAP.md](../../ROADMAP.md#design-rules)). Each unit ships its seam with a default implementation; options that provide something better replace the default. See the [catalog index](README.md).

| Unit | Seam | Default | Required by | Replaced by |
|---|---|---|---|---|
| [`current-user`](#current-user) | `ICurrentUser` | anonymous | idempotency keys, audit, logging, realtime | authentication options |
| [`model-rules`](#model-rules) | model rules run from `OnModelCreating` | – | PostgreSQL, soft delete, audit | – |
| [`cache`](#cache) | `IDistributedCache` | in memory | idempotency keys, Redis | Redis |
| [`hybrid-cache`](#hybridcache) | `HybridCache` | Microsoft's | Redis, FusionCache | FusionCache |
| [`after-commit`](#after-commit) | `IAfterCommit` | – | HybridCache (invalidation), the after-commit publisher, SignalR | – |
| [`integration-events`](#integration-events) | `IIntegrationEventPublisher`, `IMessageDispatcher` | in-process | outbox, queue options | queue options (dispatcher) |
| [`message-broker`](#message-broker) | what the queue options share (settings, message format, after-commit publisher) | – | RabbitMQ, Service Bus | – |
| [`authentication`](#authentication) | what the authentication options share (claims-based `ICurrentUser`, fallback policy) | – | Keycloak, Entra ID, Identity | – |

They're built together with the first option that needs them.

## Current user

**What.** Who is making the request, for options that record or scope things per user.

**Built (phase 6)**, with idempotency keys: `Infrastructure/CurrentUser/`, registered in `AddInfrastructure`.

**Design.**

- `ICurrentUser` with `string? UserId` and `bool IsAuthenticated`. The ID is a string because each identity provider shapes it differently.
- **In Infrastructure, not Core**, like the integration-event contracts: the domain never asks who the user is, and the lowest future consumer (audit, a save step) is in Infrastructure. Authentication options implement it in the Api, where the claims are.
- The default implementation is anonymous: `UserId` is `null`, `IsAuthenticated` is `false`. Options that use it must work with that (no auth selected, or work done by a background job).
- Authentication options replace the default with one that reads the claims. Options that use the seam never know which provider is selected (rule 1).
- Selecting an authentication option alone also adds the unit, since it provides the implementation.

**Feature template.** No change.

## Model rules

**What.** A hook that lets options configure the whole EF model (every entity with a marker interface, every table) without editing `AppDbContext` or the type configurations.

**Built (phase 5)**, earlier than planned: PostgreSQL needed it first.

**Design.**

- `IModelRule` (`Persistence/ModelRules/`): `void Apply(ModelBuilder modelBuilder)`.
- `OnModelCreating` runs every rule class found by scanning, after the type configurations, in order of full type name. That's one conditional line in `AppDbContext`, present only when the unit is.
- Rules are created with `Activator`, with no dependency injection: `OnModelCreating` runs once per model, before any scope exists.
- A rule applies what its option needs: a collation, table names, a query filter, extra columns, a table.
- Used by: PostgreSQL (`CaseInsensitiveCollation`, `SnakeCaseTableNames`, the sample `UserEmailCollationRule`). Next: soft delete and audit.

**Feature template.** No change.

## Cache

**What.** `IDistributedCache`, for options that store short-lived data shared across instances when there is a shared store.

**Built (phase 6)**, with idempotency keys: one line in `AddInfrastructure`, no files of its own. In memory (`AddDistributedMemoryCache()`), or Redis with the Redis option (`AddRedisDistributedCache("cache")`, the Aspire client): the same line, a two-way conditional.

**Design.**

- Idempotency keys store their records in `IDistributedCache`, so they pick Redis up with no glue.
- Required by idempotency keys and Redis, **not by FusionCache**: FusionCache alone keeps its values in memory and would only get a redundant second level.
- The phase 6 question (would Microsoft's `HybridCache` use the in-memory `IDistributedCache` as a redundant second level?) doesn't arise: Microsoft's `HybridCache` only exists with Redis, which replaces the in-memory one.

**Feature template.** Caches nothing.

## HybridCache

**What.** `HybridCache`, the seam features cache through (memory first, then the second level).

**Built (phase 7)**, with Redis and FusionCache: `AddInfrastructure`, a three-way conditional.

**Design.**

- **Microsoft's** (`AddHybridCache()`) with Redis alone; it finds the Redis `IDistributedCache` as its second level by itself.
- **FusionCache** (`AddFusionCache().AsHybridCache()`) with the FusionCache option. Code depends on `HybridCache` only, so features don't change with the implementation (rule 1).
- **FusionCache with Redis**, from the `fusioncache-redis` glue unit: `WithRegisteredDistributedCache()`, the System.Text.Json serializer, and the Redis backplane (`WithStackExchangeRedisBackplane`, the same connection string).
- Requires [`after-commit`](#after-commit): removing cached copies correctly needs it, so the caching options bring it.
- **Verified in phase 7, with two test hosts sharing one Redis container:** Microsoft's `HybridCache` has no backplane. After a removal on one instance, the other kept its copy in memory. With FusionCache + Redis, the other instance dropped it within the polling window. Both behaviors are tests (`Caching/HybridCacheTests.cs`); removing the backplane line makes the FusionCache test fail.

**Feature template.** Caches nothing; `docs/caching.md` shows caching a read and removing it after the commit. **Samples:** `GetUserById` reads through `HybridCache`; `UserUpdatedDomainEvent` (raised by `UpdateName` / `UpdateEmail`, new in the sample domain) and `UserDeletedDomainEvent` remove it through `ForgetCachedUserHandler`. `GetUserById` has 14 members in the template (cache × CQRS / repository / result), one in a generated solution.

## After commit

**What.** Work that must only happen once the data is saved: removing a cached copy, sending a message to a broker, a realtime notification.

**Built (phase 7)**, as a hidden unit, not in the base: the base has nothing that uses it (rule 10). `Infrastructure/Persistence/AfterCommit/`.

**Design.**

- `IAfterCommit.Enqueue(Func<CancellationToken, Task>)`, scoped. A save pipeline step (`AfterCommitStep`, order 1000) runs the queue in `AfterCommitAsync` and clears it in `SaveFailedAsync`; both hooks were already in the pipeline since phase 1.
- The data is already saved when the actions run: a failing action is logged, and the others still run.
- "After the commit" relies on each `SaveChangesAsync` being its own transaction. Code that opens an explicit transaction would see the actions run before its commit; documented.
- Unit tests call the step directly (`Persistence/AfterCommitTests.cs`): order, dropped on failure, failures isolated, run once.
- **`IsSaving` (added in phase 7d, for SignalR):** `SaveInProgressStep` (order `int.MinValue`, so domain event handlers already see it) sets it before the save and clears it after the commit or the failure. The notifier queues during a save and sends at once outside one; without it, a message sent outside a save would wait for a save that may never come. Unit-tested on the step.
- The after-commit publisher checks `IsSaving` the same way (fixed in phase 7d): before, an integration event published outside a save waited for a save that might never come. Broker tests cover both paths.

## Integration events


**What.** Events meant for the outside world: other services, emails, anything that can happen after the save and be retried. Never selected by the user: the resolver adds it when the outbox or a queue option is selected.

Domain events and integration events:

| | Domain events (base) | Integration events |
|---|---|---|
| Purpose | Keep the app consistent | Side effects and other services |
| When | Before the commit, same transaction | After the commit |
| If a handler fails | The whole save fails | Retried; the data is already saved |
| Handlers may | Change tracked entities, never call `SaveChanges` | Do anything, including external calls |

**Built (phase 4)**, in `Infrastructure/IntegrationEvents/`; documented for users in `docs/integration-events.md`.

**Design.**

- **In Infrastructure, not Core.** Nothing in Core uses them: entities raise domain events, and everything that reacts to those (domain event handlers, endpoints) is in Infrastructure or Api. At scale, event records shared with other services belong in a separate contracts project.
- For app code (public): `IIntegrationEvent`, `IIntegrationEventHandler<T>`, `IIntegrationEventPublisher`, `[IntegrationEventName]`.
- Machinery (internal):
  - **Registry** (`IntegrationEventTypes`): the `IIntegrationEvent` types in the Infrastructure assembly plus the event type of every registered handler. Built on first use from the final registrations, so tests can add events with handlers. A stored name is only ever turned back into one of these types. The name is the class name unless `[IntegrationEventName]` sets one; duplicates fail at startup.
  - **Serializer**: JSON with web defaults. An unknown name raises `UnknownIntegrationEventException`, so the outbox fails the row without retrying.
  - **`IMessageDispatcher`**, the transport. The default, `InProcessMessageDispatcher`, runs every handler of the event, each in its own scope, with `IIntegrationEventHandlerFilter`s around each call (the inbox is one). All handlers run even when one fails; the failures are thrown together, so the message is retried.
- A domain event handler publishes the integration event; see the samples' `PublishUserCreatedHandler`.
- **The after-commit publisher (built in phase 7b)**, `AfterCommitPublisher` in the [`message-broker`](#message-broker) unit: without the outbox, the event is sent through [`IAfterCommit`](#after-commit) once the save commits.
- **The handler runner (split out in phase 7b)**: `IntegrationEventHandlers` runs the handlers with their scopes and filters; `IMessageDispatcher` only delivers (in process it calls the runner, with a broker it sends and the consumer calls the runner).
- Integration events are public contracts: add fields, never rename or remove them.

**Feature template.** Generates none. Integration events are deliberate contracts; the docs show how to add one.

**Samples.** See the [outbox](patterns.md#outbox): three integration events, with `samples` + `integration_events`.


## Message broker

**What.** What the queue options share, so each broker option only brings its own client code.

**Built (phase 7b)**, with RabbitMQ: `Infrastructure/IntegrationEvents/Broker/`.

**Design.**

- `BrokerOptions` (`Messaging:` section): `Topic` and `Subscription` (a RabbitMQ exchange and queue, a Service Bus topic and subscription), `MaxDeliveries`, `Prefetch`.
- `BrokerMessage`: the message format every broker uses (JSON body, event name as the type, message ID, `traceparent`).
- `AfterCommitPublisher`, only without the outbox (excluded with it): sends each event once the save commits.
- Requires `integration-events` and `after-commit`. With the outbox, `after-commit` is present but unused; accepted (see [infrastructure.md](infrastructure.md#queue)).
- Its symbol, `message_broker`, is what code and tests use for "any broker" (`#if (!message_broker)` on the outbox's in-process-only test).

## Authentication

**What.** What the authentication options share, so each option only brings its own scheme.

**Built (phase 7c)**, with Keycloak: `Api/Configuration/Authentication/`.

**Design.**

- `ClaimsCurrentUser` replaces the anonymous `ICurrentUser` of the [`current-user`](#current-user) unit, which it requires: the user ID is the name identifier claim. Outside a request (background work) it answers anonymous.
- A fallback policy requires an authenticated user on every endpoint; health checks and the OpenAPI document are anonymous.
- Requires `current-user`. It doesn't provide `login`: each option does, so an option that needs logged-in users never gets this unit alone (see [infrastructure.md](infrastructure.md#authentication)).
- Its symbol, `authentication`, is what code and tests use for "any authentication option".

**Feature template.** No change.
