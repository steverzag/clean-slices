#if (cqrs)
namespace CleanSlices.Core.Domain;

// What queries read from: nothing loaded through it is tracked, so it can't be changed and saved.
public interface IReadDb
{
	public IQueryable<T> Query<T>() where T : class;
}
#endif
