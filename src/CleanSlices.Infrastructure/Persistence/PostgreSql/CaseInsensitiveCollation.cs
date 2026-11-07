#if (postgresql)
using CleanSlices.Infrastructure.Persistence.ModelRules;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence.PostgreSql;

// Compares text ignoring case but not accents, like SQL Server's default collation. Give it to a column with
// UseCollation(CaseInsensitiveCollation.Name). PostgreSQL can't use LIKE (Contains, StartsWith) on such a column.
internal sealed class CaseInsensitiveCollation : IModelRule
{
	public const string Name = "case_insensitive";

	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.HasCollation(Name, locale: "und-u-ks-level2", provider: "icu", deterministic: false);
}
#endif
