# Audit

Every change to an auditable entity is recorded in the **`AuditEntries`** table, in the same transaction as the change: if the save fails, nothing is recorded. Entities get no extra fields. The work is in `Infrastructure/Persistence/Audit/`: `AuditStep` (save pipeline, order 300) writes the entries, and `AuditEntry` is the table.

| Column | |
|---|---|
| `EntityType` | the class's short name (`TaskItem`) |
| `EntityId` | the key (the `Id`; several key values are joined with commas) |
| `Action` | `Added`, `Modified` or `Deleted` |
| `Before` | JSON of the entity before the change; `null` when added |
| `After` | JSON of the entity after the change; `null` when deleted |
| `UserId` | `ICurrentUser.UserId`: `null` for anonymous requests and background work |
| `Timestamp` | when the save ran (UTC) |
| `TraceId` | the request's trace, to find it in the logs and traces |

The snapshots hold the mapped columns as EF tracks them (original values for `Before`, current values for `After`), with enums as strings. They never include the domain events, computed properties or related collections.

<!--#if (soft_delete)-->
With soft delete, a delete is an update: the entry is `Modified`, with `DeletedAt` set in `After`. "Who deleted it" comes for free.

<!--#endif-->
## Auditing an entity

`add feature` does it for new entities. For an existing one, implement the empty marker `IAuditable` (`Core/Domain/`):

```csharp
public class Product : Entity, IAuditable
```

Changes are recorded from then on. A row that existed before has no `Added` entry; its first change records its full `Before`, so its history still starts complete.

To leave a property out of the snapshots, mark it `[NotAudited]`: personal data you don't want copied, or large values.

<!--#if (samples)-->
In the samples, tasks and users are audited, and `User.Email` is `[NotAudited]`.

<!--#endif-->
## Reading the history

```csharp
var history = await context.Set<AuditEntry>()
	.Where(e => e.EntityType == nameof(TaskItem) && e.EntityId == id.ToString())
	.OrderBy(e => e.Timestamp)
	.ToListAsync(cancellationToken);
```

The index on `(EntityType, EntityId, Timestamp)` makes this cheap. "Created by" is the first entry's `UserId`, and "last modified by" the latest one's; there are no columns for them on the entities.

## Trade-offs

- **Two full snapshots per change** take more space than a list of changed values, but each entry stands on its own, and the first change to an older row is complete.
- **`Before` is right for entities loaded from the database and then changed**, which is what the handlers do. An entity attached without loading (`context.Update(detached)`) has no original values to record.
- **Related collections aren't in the snapshot.** Audit the related entities themselves (give them the marker).
- **`EntityType` is the class name**: renaming the class splits its history. Update the old entries when you rename.
- **Bulk operations** (`ExecuteUpdate`, `ExecuteDelete`) skip the save pipeline and aren't audited.
- **Personal data.** The snapshots copy what they audit. Use `[NotAudited]`, and include `AuditEntries` when you purge a person's data (GDPR):

```csharp
await context.Set<AuditEntry>()
	.Where(e => e.EntityType == nameof(User) && e.EntityId == id.ToString())
	.ExecuteDeleteAsync(cancellationToken);
```

There is no automatic clean-up: audit data often has to be kept for a set time. Delete old entries yourself when your rules allow it.
