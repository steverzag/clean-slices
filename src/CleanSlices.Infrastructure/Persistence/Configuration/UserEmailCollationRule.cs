#if (postgresql)
using CleanSlices.Core.Domain.Users;
using CleanSlices.Infrastructure.Persistence.ModelRules;
using CleanSlices.Infrastructure.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence.Configuration;

// Emails are unique ignoring case, as with SQL Server, so "Ana@x.com" and "ana@x.com" are the same user.
internal sealed class UserEmailCollationRule : IModelRule
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<User>().Property(u => u.Email).UseCollation(CaseInsensitiveCollation.Name);
}
#endif
