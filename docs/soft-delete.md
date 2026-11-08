# Soft delete

Deleting a soft-deletable entity sets its `DeletedAt` instead of removing the row, and deleted rows are hidden from every query. The API behaves exactly as after a real delete: `GET` returns 404, a second `DELETE` returns 404, and a unique value (an email) can be used again.

Nothing changes in handlers: they call the entity's `Delete()` and `Remove()` as before, and the domain events run. The work is in `Infrastructure/Persistence/SoftDelete/`:

- **`SoftDeleteStep`** (save pipeline, order 100, after domain events): a deleted `ISoftDeletable` entity becomes an update that sets `DeletedAt` to the current time. A delete made by a domain event handler is converted too.
- **`SoftDeleteRule`** (a model rule): a query filter named `SoftDelete` on every soft-deletable entity, and a filter on its unique indexes (below).

The filter applies to every query: `context.Set<T>()`, `FindAsync`, and the repositories and `IReadDb` when those options are selected.

## Making an entity soft-deletable

`add feature` does it for new entities. For an existing one, implement `ISoftDeletable` (`Core/Domain/`) with a `DeletedAt` only the save pipeline sets:

```csharp
public class Product : Entity, ISoftDeletable
{
	public DateTimeOffset? DeletedAt { get; private set; }
	// ...
}
```

Then create a migration: it adds the column and changes the entity's unique indexes.

```
dotnet ef migrations add SoftDeleteProducts --project src/CleanSlices.Infrastructure
```

## Reading deleted rows

Switch the filter off by name, which keeps any other filter in place:

```csharp
var deleted = await context.Set<User>()
	.IgnoreQueryFilters([SoftDeleteFilter.Name])
	.Where(u => u.DeletedAt != null)
	.ToListAsync(cancellationToken);
```

## Unique indexes

A deleted row keeps its values, so a plain unique index would refuse a new user with a deleted user's email. Worse, the handler's check ("is this email in use?") doesn't see the deleted row, so the insert would fail with a 500. The model rule therefore limits every unique index of a soft-deletable entity to the rows that aren't deleted:

<!--#if (postgresql)-->
- PostgreSQL: a partial index, `WHERE "deleted_at" IS NULL`.
<!--#else-->
- SQL Server: a filtered index, `WHERE [DeletedAt] IS NULL` (plus `[Column] IS NOT NULL` for nullable columns, which EF otherwise adds itself).
<!--#endif-->

This covers your own unique indexes too, with no configuration. Restoring a row whose value has been taken again fails on the index; check for it first.

## Related rows

Soft-deleting a row only marks it. The database's cascade (`OnDelete(DeleteBehavior.Cascade)`) runs only for real deletes, so rows that depend on it stay:
<!--#if (samples)-->
a deleted task keeps its user assignments, and gets them back when it's restored.
<!--#else-->
they come back with it when it's restored.
<!--#endif-->

They're hidden when reached through the deleted row. If you query them on their own, give them the marker too, or filter by their parent.

EF logs a warning (10622) when a soft-deletable entity is the required end of a relationship: queries for the dependents can't see the hidden parent. It's expected here; make the navigation optional or filter the dependents too if it matters for your queries.

## Restoring

There is no endpoint; restore from your own code:

```csharp
var user = await context.Set<User>()
	.IgnoreQueryFilters([SoftDeleteFilter.Name])
	.SingleAsync(u => u.Id == id, cancellationToken);

context.Entry(user).Property(u => u.DeletedAt).CurrentValue = null;
await context.SaveChangesAsync(cancellationToken);
```

## Purging

Soft delete keeps personal data, which privacy rules (GDPR) may require you to remove. Purge deleted rows for good with a bulk delete, which skips the save pipeline and so really deletes:

```csharp
await context.Set<User>()
	.IgnoreQueryFilters([SoftDeleteFilter.Name])
	.Where(u => u.DeletedAt < DateTimeOffset.UtcNow.AddDays(-30))
	.ExecuteDeleteAsync(cancellationToken);
```

<!--#if (audit)-->
Audit entries copy the data too: purge the person's `AuditEntries` as well (see [audit.md](audit.md)).

<!--#endif-->
Bulk operations (`ExecuteDelete`, `ExecuteUpdate`) always skip the save pipeline: an `ExecuteDelete` without `IgnoreQueryFilters` only reaches rows that aren't deleted, and deletes them for good.

## Large tables

Queries filter on `DeletedAt IS NULL`. On a large table, an index that holds only the rows that aren't deleted keeps them fast; add it in the entity's type configuration:

<!--#if (postgresql)-->
```csharp
builder.HasIndex(p => p.CreatedAt).HasFilter("\"deleted_at\" IS NULL");
```
<!--#else-->
```csharp
builder.HasIndex(p => p.CreatedAt).HasFilter("[DeletedAt] IS NULL");
```
<!--#endif-->

There is no `IsDeleted` column: it would hold the same fact as `DeletedAt` and could disagree with it, and it wouldn't make the filter faster.
