# Strategies

A strategy adds *behavior to entities*. Any number can be selected. See the [catalog index](README.md).

## How strategies plug in

Every strategy follows the same three steps, so strategies never touch each other or `AppDbContext`:

1. **A marker interface in Core** (`ISoftDeletable`, `IAuditable`), implemented by the entities that use the strategy.
2. **A model rule**, found by scanning and run from the `OnModelCreating` hook of the [`model-rules`](hidden-units.md#model-rules) hidden unit, applies filters, columns or tables to every entity with the marker.
3. **A save step** in the ordered save pipeline fills values or writes rows automatically (see [base-template.md](base-template.md#ordered-save-pipeline)).

**Which entities get a strategy.** `add feature` adds the marker to new entities when the strategy is selected. The CLI doesn't convert existing entities, since that would edit the user's files (rule 4). The docs show how: add the interface, then a migration.

**Bulk operations** (`ExecuteUpdate`, `ExecuteDelete`) skip the save pipeline, so strategies don't apply to them.

## Soft delete

**What.** Deleting an entity sets `DeletedAt` instead of removing the row, and deleted rows are hidden from queries.

**Built (phase 8).** Documented for users in `docs/soft-delete.md`.

**As built.**

- `ISoftDeletable` in `Core/Domain`; entities declare `public DateTimeOffset? DeletedAt { get; private set; }`. `SoftDeleteStep` (order 100) and `SoftDeleteRule` (named filter `SoftDelete`, public as `SoftDeleteFilter.Name` for app code, built as an expression per entity type: EF 10's `HasQueryFilter(name, LambdaExpression)`) in `Infrastructure/Persistence/SoftDelete/`. Requires `model-rules`.
- **Filtered unique indexes, automatic** (changed from the design, where they were a trade-off to document): with a plain unique index, the handler's "email in use" check doesn't see the deleted user, the insert hits the index, and the client gets a 500 where a hard delete gives 201 (rule 7). The rule filters every unique index of a soft-deletable entity: `[DeletedAt] IS NULL` on SQL Server, plus `[Column] IS NOT NULL` for nullable columns (EF adds that itself only for indexes without a filter of their own), `"deleted_at" IS NULL` on PostgreSQL (one `#if (postgresql)`; the snake_case name is the template's own convention). A test that runs in every variant, `A_deleted_users_email_can_be_used_again`, fails with a 500 without the filter (sabotage-proved).
- **Related rows stay** (changed from what was expected in phase 8's discussion): the delete handlers load the entity without its children, and the database cascade only runs on a real `DELETE`, so a soft-deleted task keeps its assignments and gets them back on restore. A test pins it.
- **CLI**: the migration message became "changes the database" (soft delete adds a column and changes indexes), and units can carry an `addNote`, printed after `add`: soft delete's says existing entities aren't converted (rule 4).
- Samples: `User` and `TaskItem` are soft-deletable. Feature template: `Widget` implements `ISoftDeletable`.

**Design.**

- `ISoftDeletable` has a read-only `DateTimeOffset? DeletedAt`. Domain code can read it; only the save step sets it.
- The save step (order 100) turns a deleted `ISoftDeletable` entity into an update and sets `DeletedAt` to the current time. Handlers and the entity's `Delete()` don't change.
- A named EF Core query filter, `"SoftDelete"`, hides deleted rows everywhere, including repositories and CQRS's `IReadDb`. It's switched off by name: `IgnoreQueryFilters(["SoftDelete"])`, which leaves other filters in place.
- Rule 7: the API behaves exactly like a hard delete. After a delete, GET returns 404 and a second DELETE returns 404.
- No `IsDeleted` column: it would hold the same fact as `DeletedAt` and could disagree with it, and it doesn't make the filter faster.

**Feature template.** The entity implements `ISoftDeletable` and has `DeletedAt`.

**Samples.** *Proposed:* tasks and users are soft-deletable. Deleting a user still unassigns them (the domain event runs), and a test shows the API answers 404 as after a hard delete.

**Interactions.** Audit records the soft delete as a `Modified` entry with `DeletedAt` set, so "who deleted it" comes for free. `WidgetDeleted` is still raised, so cache invalidation works unchanged.

**Trade-offs to document.**

- Restore: example code for undeleting.
- Purging and GDPR: soft delete keeps personal data. The docs show a purge with `IgnoreQueryFilters` and `ExecuteDelete`, covering `AuditEntries` too.
- Unique indexes: a deleted row's value blocks reuse. The fix is a filtered unique index, written per provider: `[DeletedAt] IS NULL` on SQL Server, `"deleted_at" IS NULL` on PostgreSQL.
- Performance on large tables: a filtered (SQL Server) or partial (PostgreSQL) index containing only non-deleted rows.
- EF warns when a required navigation points to a filtered entity; the docs explain the fix.

## Audit

**What.** Every change to an audited entity is recorded in an `AuditEntries` table, with full snapshots of the entity before and after. Entities get no extra fields.

**Built (phase 8).** Documented for users in `docs/audit.md`.

**As built.**

- `IAuditable` and `[NotAudited]` in `Core/Domain`; `AuditEntry`, its type configuration and `AuditStep` (order 300) in `Infrastructure/Persistence/Audit/`. Requires `current-user` only: the table is a type configuration found by the usual scan, so no model rule (changed from the shared "how strategies plug in" steps).
- `EntityId` is a string (the key values joined), so any key works; `Action` stored as a string. Snapshots: mapped properties from EF's entries, enums as strings, keys as the property names.
- Tests (samples, `Features/Tasks/AuditTests`): a task's history (added, renamed, completed, with before and after, the trace and the user: the test user's ID with an authentication option, `null` without); a delete (`Modified` with `DeletedAt` under soft delete, `Deleted` otherwise); `User.Email` is `[NotAudited]` in the samples and absent from the snapshot. Sabotage-proved: audit ordered before soft delete records a `Deleted`; ignoring the attribute shows the email.
- No sample endpoint for the history (it would be 8 handler variants); `docs/audit.md` shows the query.

**Design.**

- `IAuditable` is an empty marker.
- The save step (order 300) reads `ChangeTracker.Entries<IAuditable>()` and writes one entry per change, in the same transaction:

  ```
  AuditEntries: Id, EntityType, EntityId, Action (Added / Modified / Deleted),
                Before (JSON, null when Added), After (JSON, null when Deleted),
                UserId, Timestamp, TraceId
  Index: (EntityType, EntityId, Timestamp)
  ```

- Snapshots are built from EF's change tracking: original values for Before, current values for After. They contain only mapped columns, never the domain-event list or computed properties.
- Properties marked `[NotAudited]` are left out of both snapshots.
- `UserId` comes from `ICurrentUser`; `null` means anonymous or the system.
- The JSON columns are plain strings, so they work on both database providers.

**Feature template.** The entity implements `IAuditable`.

**Samples.** *Proposed:* tasks and users are audited; a test reads a task's audit entries after a few updates.

**Trade-offs to document.**

- "Created by" and "last modified by" aren't columns; they come from the latest entries. The index makes that query cheap.
- Two full snapshots per change take more space than diffs. Before is kept so the first change to a row that existed before auditing is complete.
- Before is only correct for entities loaded from the database, then changed. The template's handlers always do that.
- Snapshots don't include related collections; related entities need the marker themselves.
- `EntityType` is the class's short name; renaming the class splits its history.
- Snapshots copy personal data: use `[NotAudited]`, and include the table in GDPR purges. There's no automatic clean-up, since audit data is often required to be kept.

## Parked: optimistic concurrency

Not approved; kept here so it can be picked up later.

**Problem.** Two clients load the same entity and both save: the second save silently overwrites the first. A token checked only on the server protects just the few milliseconds between load and save inside one request; real protection needs the client to send back the version it read.

**Proposed design.**

- `IVersioned` with a read-only `Guid Version`, which the save step (order 200) changes on every add, update and soft delete. It's EF's concurrency token, provider-neutral (no `rowversion` or `xmin`).
- `GET /widgets/{id}` returns the version in an `ETag` header.
- `.WithIfMatch()` on PATCH and DELETE reads `If-Match` and the route `{id}` into a request-scoped service. The save step returns **412** if the loaded version differs, before writing anything. A save racing between our load and save fails EF's check and returns **409**. A successful PATCH returns the new `ETag`.
- `If-Match` is optional by default; `Concurrency:RequireIfMatch` makes it mandatory, and a missing header returns **428**.
- The base `ExceptionHandler` maps EF's concurrency exception to 409 in every variant.
- Filter order: validation, idempotency, If-Match, so a replayed request isn't refused with 412.
