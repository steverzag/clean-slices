using CleanSlices.Api.Tests.Infrastructure;
using CleanSlices.Infrastructure;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(ApiFactory))]

namespace CleanSlices.Api.Tests.Infrastructure;

/// <summary>
/// Runs the API in memory against a SQL Server container, shared by every test in the run.
/// Tests create their own data and never reset the database, so they don't depend on each other.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
	private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

	public async ValueTask InitializeAsync()
	{
		await _database.StartAsync(TestContext.Current.CancellationToken);
		await CreateSchemaAsync(this);
	}

	/// <summary>
	/// A separate API host with its own empty database, for tests that count rows (totals, pages).
	/// The shared database can't be used for that: other tests add rows to it at the same time.
	/// </summary>
	public async Task<WebApplicationFactory<Program>> WithEmptyDatabaseAsync()
	{
		var databaseName = $"CleanSlicesTests_{Guid.NewGuid():N}";
		var factory = WithWebHostBuilder(builder => UseDatabase(builder, databaseName));
		await CreateSchemaAsync(factory);
		return factory;
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		UseDatabase(builder, "CleanSlicesTests");
	}

	private void UseDatabase(IWebHostBuilder builder, string databaseName)
	{
		var connectionString = new SqlConnectionStringBuilder(_database.GetConnectionString())
		{
			InitialCatalog = databaseName
		};

		builder.UseSetting(
			$"ConnectionStrings:{InfrastructureRegistration.DatabaseConnectionName}",
			connectionString.ConnectionString);
	}

	// Generated solutions don't ship migrations, so the schema comes straight from the model.
	private static async Task CreateSchemaAsync(WebApplicationFactory<Program> factory)
	{
		using var scope = factory.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
	}

	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync();
		await _database.DisposeAsync();
	}
}
