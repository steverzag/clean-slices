#if (soft_delete)
namespace CleanSlices.Infrastructure.Persistence.SoftDelete;

// The query filter that hides deleted rows: IgnoreQueryFilters([SoftDeleteFilter.Name]) shows them.
public static class SoftDeleteFilter
{
	public const string Name = "SoftDelete";
}
#endif
