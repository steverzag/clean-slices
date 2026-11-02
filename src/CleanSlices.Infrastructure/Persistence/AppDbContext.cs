//#if (samples)
using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Domain.Users;
//#endif
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence;

public class AppDbContext : DbContext
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
	}
}
