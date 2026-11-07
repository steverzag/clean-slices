#if (repository || cqrs)
using CleanSlices.Core.Domain;
#endif
//#if (samples)
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Domain.Users;
//#endif
#if (model_rules)
using CleanSlices.Infrastructure.Persistence.ModelRules;
#endif
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence;

#if (repository && cqrs)
public class AppDbContext : DbContext, IUnitOfWork, IReadDb
#elif (repository)
public class AppDbContext : DbContext, IUnitOfWork
#elif (cqrs)
public class AppDbContext : DbContext, IReadDb
#else
public class AppDbContext : DbContext
#endif
{
	//#if (samples)
	public DbSet<User> Users => Set<User>();
	public DbSet<TaskItem> Tasks => Set<TaskItem>();

	//#endif
	public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
#if (model_rules)
		modelBuilder.ApplyModelRules();
#endif
	}
#if (cqrs)

	IQueryable<T> IReadDb.Query<T>() => Set<T>().AsNoTracking();
#endif
}
