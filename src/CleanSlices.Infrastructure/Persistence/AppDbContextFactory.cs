using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CleanSlices.Infrastructure.Persistence;

internal class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{

	// Only used for design-time DbContext creation (e.g., for migrations)
	public AppDbContext CreateDbContext(string[] args)
	{
		var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
		// Try to get connection string from environment variable (ASP.NET Core format)
		var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
			?? "Server=localhost;Database=CleanSlices;Trusted_Connection=True;TrustServerCertificate=True";
		optionsBuilder.UseSqlServer(connectionString);

		return new AppDbContext(optionsBuilder.Options);
	}
}
