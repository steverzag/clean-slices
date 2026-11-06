using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Pagination;

public class PaginationList<T> : ReadOnlyCollection<T>
{
	public int PageNumber { get; }
	public int PageSize { get; }
	public int TotalCount { get; }
	public int TotalPages { get; }

	private PaginationList(List<T> items, int totalCount, int pageNumber, int pageSize) : base(items)
	{
		PageNumber = pageNumber;
		PageSize = pageSize;
		TotalCount = totalCount;
		TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
	}

	public bool HasPreviousPage => PageNumber > 1;

	public bool HasNextPage => PageNumber < TotalPages;

	internal static async Task<PaginationList<T>> CreateAsync(IQueryable<T> source, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

		var count = await source.CountAsync(cancellationToken);
		var items = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
		return new PaginationList<T>(items, count, pageNumber, pageSize);
	}
}
