namespace CleanSlices.Infrastructure.Pagination;

public static class PaginationExtensions
{
	extension<T>(IQueryable<T> source)
	{
		public Task<PaginationList<T>> PaginateAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
			PaginationList<T>.CreateAsync(source, pageNumber, pageSize, cancellationToken);

		public Task<PaginationList<T>> PaginateAsync(PaginationParameters parameters, CancellationToken cancellationToken = default) =>
			PaginationList<T>.CreateAsync(source, parameters.PageNumber, parameters.PageSize, cancellationToken);
	}
}
