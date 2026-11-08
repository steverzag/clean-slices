#if (soft_delete)
namespace CleanSlices.Core.Domain;

// Deleting the entity sets DeletedAt instead of removing the row, and deleted rows are hidden from queries.
// Only the save pipeline sets it; Delete() and Remove() don't change.
public interface ISoftDeletable
{
	public DateTimeOffset? DeletedAt { get; }
}
#endif
