#if (postgresql)
using System.Text;
using CleanSlices.Infrastructure.Persistence.ModelRules;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence.PostgreSql;

// UseSnakeCaseNamingConvention renames columns and conventional table names, but keeps names set with ToTable.
// This renames those too, so the whole schema is snake_case and never needs quotes.
internal sealed class SnakeCaseTableNames : IModelRule
{
	public void Apply(ModelBuilder modelBuilder)
	{
		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			if (entityType.GetTableName() is { } table)
			{
				entityType.SetTableName(ToSnakeCase(table));
			}
		}
	}

	private static string ToSnakeCase(string name)
	{
		var result = new StringBuilder(name.Length + 8);
		for (var i = 0; i < name.Length; i++)
		{
			var c = name[i];
			if (char.IsUpper(c) && i > 0 && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
			{
				result.Append('_');
			}

			result.Append(char.ToLowerInvariant(c));
		}

		return result.ToString();
	}
}
#endif
