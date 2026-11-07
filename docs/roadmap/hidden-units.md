# Hidden units

A hidden unit is a piece that several options need but nobody picks on its own. The resolver adds it when a selected option `requires` it, so it's never in a solution that doesn't use it (rule 10 in [ROADMAP.md](../../ROADMAP.md#design-rules)). Each unit ships its seam with a default implementation; options that provide something better replace the default. See the [catalog index](README.md).

| Unit | Seam | Default | Required by | Replaced by |
|---|---|---|---|---|
| [`current-user`](#current-user) | `ICurrentUser` | anonymous | idempotency keys, audit, logging, realtime | authentication options |
| [`model-rules`](#model-rules) | model rules run from `OnModelCreating` | – | PostgreSQL, soft delete, audit | – |
| [`cache`](#cache) | `HybridCache` + `IDistributedCache` | in memory | idempotency keys, Redis | Redis (shared second level) |
| [`integration-events`](#integration-events) | `IIntegrationEventPublisher`, `IMessageDispatcher` | in-process | outbox, queue options | queue options (dispatcher) |

They're built together with the first option that needs them.

## Current user

**What.** Who is making the request, for options that record or scope things per user.

**Design.**

- `ICurrentUser` with `string? UserId` and `bool IsAuthenticated`. The ID is a string because each identity provider shapes it differently.
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

**What.** The caching seam, for options that store short-lived data.

**Design.**

- `AddHybridCache()` plus the in-memory `IDistributedCache`.
- Idempotency keys store their records in `IDistributedCache`. The Redis option registers a Redis `IDistributedCache`, and `HybridCache` uses it as its second level: both pick Redis up with no glue.
- Verify when building: `HybridCache` should not use the in-memory `IDistributedCache` as a redundant second level.

**Feature template.** Caches nothing; see [infrastructure.md](infrastructure.md#cache) for the recommended way to add caching to an endpoint.

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
- **Not built yet: the after-commit publisher.** Without the outbox, `IIntegrationEventPublisher` would buffer events and send them in the save pipeline's after-commit phase. Only a queue option (phase 7) reaches that case, so it's built then. Until phase 7, the outbox provides the only publisher.
- Integration events are public contracts: add fields, never rename or remove them.

**Feature template.** Generates none. Integration events are deliberate contracts; the docs show how to add one.

**Samples.** See the [outbox](patterns.md#outbox): three integration events, with `samples` + `integration_events`.

