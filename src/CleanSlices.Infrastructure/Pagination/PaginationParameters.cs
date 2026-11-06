namespace CleanSlices.Infrastructure.Pagination;

/// <summary>
/// Which page to read: <c>?pageNumber=2&amp;pageSize=10</c> when bound with <c>[AsParameters]</c>.
/// Out-of-range values are clamped, never rejected.
/// </summary>
/// <remarks>
/// The values come through the constructor because <c>[AsParameters]</c> treats constructor parameters with
/// defaults as optional; settable <c>int</c> properties would be required, and a missing value would be a 400.
/// </remarks>
public class PaginationParameters(int pageNumber = 1, int pageSize = PaginationParameters.DefaultPageSize)
{
	public const int DefaultPageSize = 10;
	public const int MaxPageSize = 50;

	// Keeps (pageNumber - 1) * pageSize inside an int.
	private const int MaxPageNumber = int.MaxValue / MaxPageSize;

	public int PageNumber { get; } = Math.Clamp(pageNumber, 1, MaxPageNumber);

	public int PageSize { get; } = Math.Clamp(pageSize, 1, MaxPageSize);
}
