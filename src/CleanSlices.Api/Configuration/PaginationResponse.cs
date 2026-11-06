using CleanSlices.Infrastructure.Pagination;

namespace CleanSlices.Api.Configuration;

/// <summary>
/// The JSON shape of a page. <see cref="PaginationList{T}"/> is a collection, which JSON would write as a bare array
/// and drop the page information, so list endpoints return this instead.
/// </summary>
public sealed record PaginationResponse<T>(
	IReadOnlyList<T> Items,
	int PageNumber,
	int PageSize,
	int TotalCount,
	int TotalPages,
	bool HasPreviousPage,
	bool HasNextPage);

public static class PaginationResponseExtensions
{
	extension<T>(PaginationList<T> page)
	{
		public PaginationResponse<T> ToResponse() => new(
			[.. page],
			page.PageNumber,
			page.PageSize,
			page.TotalCount,
			page.TotalPages,
			page.HasPreviousPage,
			page.HasNextPage);
	}
}
