# Hidden units

A hidden unit is a piece that several options need but nobody picks on its own. The resolver adds it when a selected option `requires` it, so it's never in a solution that doesn't use it (rule 10 in [ROADMAP.md](../../ROADMAP.md#design-rules)). Each unit ships its seam with a default implementation; options that provide something better replace the default. See the [catalog index](README.md).

| Unit | Seam | Default | Required by | Replaced by |
|---|---|---|---|---|
| [`current-user`](#current-user) | `ICurrentUser` | anonymous | idempotency keys, audit, logging, realtime | authentication options |
| [`model-rules`](#model-rules) | model rules run from `OnModelCreating` | – | soft delete, audit | – |
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

**What.** A hook that lets strategies configure every entity with their marker interface without editing `AppDbContext`.

**Design.**

- `OnModelCreating` runs every model rule class found by scanning, after the type configurations. That's one conditional line in `AppDbContext`, present only when a strategy is selected.
- A rule receives the `ModelBuilder` and applies what its strategy needs: a query filter, extra columns, a table.

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

**Design.**

- Contains `IIntegrationEvent`, `IIntegrationEventHandler<T>`, the serializer and the type registry.
- A domain event handler publishes the integration event:
  ```csharp
  internal sealed class PublishUserCreated(IIntegrationEventPublisher publisher) : INotificationHandler<UserCreatedDomainEvent>
  {
  	public Task Handle(UserCreatedDomainEvent e, CancellationToken ct) =>
  		publisher.Publish(new UserCreatedIntegrationEvent(e.Id, e.Email), ct);
  }
  ```
- Two seams:
  - `IIntegrationEventPublisher`, what app code calls. Without the outbox it buffers the event and sends it in the save pipeline's after-commit phase; with the outbox it writes an outbox row.
  - `IMessageDispatcher`, the transport. The default calls the `IIntegrationEventHandler`s in-process, in the background; queue options send to a broker.
- Integration events are public contracts: add fields, never rename or remove them.

**Feature template.** Generates none. Integration events are deliberate contracts; the docs show how to add one.

