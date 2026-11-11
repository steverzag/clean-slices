#if (soft_delete)
using System.Linq.Expressions;
using CleanSlices.Core.Domain;
using CleanSlices.Infrastructure.Persistence.ModelRules;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence.SoftDelete;

internal sealed class SoftDeleteRule : IModelRule
{
	public void Apply(ModelBuilder modelBuilder)
	{
		var softDeletable = modelBuilder.Model.GetEntityTypes()
			.Where(t => typeof(ISoftDeletable).IsAssignableFrom(t.ClrType) && t.BaseType is null)
			.ToList();

		foreach (var entityType in softDeletable)
		{
			// Deleted rows are hidden everywhere.
			var entity = Expression.Parameter(entityType.ClrType, "entity");
			var deletedAt = Expression.Property(entity, nameof(ISoftDeletable.DeletedAt));
			var hideDeleted = Expression.Lambda(Expression.Equal(deletedAt, Expression.Constant(null, typeof(DateTimeOffset?))), entity);
			modelBuilder.Entity(entityType.ClrType).HasQueryFilter(SoftDeleteFilter.Name, hideDeleted);

			// A deleted row's value must not block a new one: the API answers as after a real delete.
			var column = entityType.FindProperty(nameof(ISoftDeletable.DeletedAt))!.GetColumnName();
			foreach (var index in entityType.GetIndexes().Where(i => i.IsUnique))
			{
#if (postgresql)
				var conditions = new[] { $"\"{column}\" IS NULL" };
#else
				// SQL Server treats NULLs as equal in a unique index. EF leaves them out by itself, but only when the
				// index has no filter of its own, so this filter has to do it.
				var conditions = index.Properties.Where(p => p.IsNullable)
					.Select(p => $"[{p.GetColumnName()}] IS NOT NULL")
					.Append($"[{column}] IS NULL");
#endif
				var filter = string.Join(" AND ", conditions);
				index.SetFilter(index.GetFilter() is { } existing ? $"({existing}) AND {filter}" : filter);
			}
		}
	}
}
#endif
