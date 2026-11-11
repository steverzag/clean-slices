#if (repository || cqrs)
using CleanSlices.Core.Domain;
#endif
//#if (samples)
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Domain.Users;
//#endif
#if (identity)
using CleanSlices.Infrastructure.Identity;
#endif
#if (model_rules)
using CleanSlices.Infrastructure.Persistence.ModelRules;
#endif
#if (identity)
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
#endif
using Microsoft.EntityFrameworkCore;
#if (soft_delete)
using Microsoft.EntityFrameworkCore.ChangeTracking;
#endif

namespace CleanSlices.Infrastructure.Persistence;

#if (identity && repository && cqrs)
public class AppDbContext : IdentityDbContext<AppUser>, IUnitOfWork, IReadDb
#elif (identity && repository)
public class AppDbContext : IdentityDbContext<AppUser>, IUnitOfWork
#elif (identity && cqrs)
public class AppDbContext : IdentityDbContext<AppUser>, IReadDb
#elif (identity)
public class AppDbContext : IdentityDbContext<AppUser>
#elif (repository && cqrs)
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
#if (identity)
	// Hides IdentityDbContext.Users: Users are the sample's domain users; login accounts are Set<AppUser>().
	public new DbSet<User> Users => Set<User>();
#else
	public DbSet<User> Users => Set<User>();
#endif
	public DbSet<TaskItem> Tasks => Set<TaskItem>();

	//#endif
	public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
	{
#if (soft_delete)
		// A soft delete must not cascade to tracked dependents: cascade at save time, after it became an update.
		ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
#endif
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
#if (identity)
		base.OnModelCreating(modelBuilder);
#endif
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
#if (model_rules)
		modelBuilder.ApplyModelRules();
#endif
	}
#if (cqrs)

	IQueryable<T> IReadDb.Query<T>() => Set<T>().AsNoTracking();
#endif
}
