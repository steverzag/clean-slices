#if (repository)
namespace CleanSlices.Core.Shared;

public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
	public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

	public bool HasPreviousPage => PageNumber > 1;

	public bool HasNextPage => PageNumber < TotalPages;

	public Page<TResult> Map<TResult>(Func<T, TResult> map) => new([.. Items.Select(map)], PageNumber, PageSize, TotalCount);
}
#endif
