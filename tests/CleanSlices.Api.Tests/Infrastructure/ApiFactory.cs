using CleanSlices.Api.Tests.Infrastructure;
#if (integration_events)
using CleanSlices.Api.Tests.IntegrationEvents;
#endif
using CleanSlices.Infrastructure;
#if (inbox)
using CleanSlices.Infrastructure.Inbox;
#endif
#if (rabbitmq)
using CleanSlices.Infrastructure.IntegrationEvents.RabbitMq;
#endif
#if (outbox)
using CleanSlices.Infrastructure.Outbox;
#endif
using CleanSlices.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
#if (integration_events)
using Microsoft.AspNetCore.TestHost;
#endif
using Microsoft.AspNetCore.Mvc.Testing;
#if (!postgresql)
using Microsoft.Data.SqlClient;
#endif
using Microsoft.Extensions.DependencyInjection;
#if (integration_events)
using Microsoft.Extensions.Hosting;
#endif
#if (postgresql)
using Npgsql;
using Testcontainers.PostgreSql;
#else
using Testcontainers.MsSql;
#endif
#if (redis)
using Testcontainers.Redis;
#endif
#if (rabbitmq)
using Testcontainers.RabbitMq;
#endif

[assembly: AssemblyFixture(typeof(ApiFactory))]

namespace CleanSlices.Api.Tests.Infrastructure;

/// <summary>
/// Runs the API in memory against a database container, shared by every test in the run.
/// Tests create their own data and never reset the database, so they don't depend on each other.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
#if (postgresql)
	private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17").Build();
#else
	private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
#endif
#if (redis)
	private readonly RedisContainer _cache = new RedisBuilder("redis:7.4").Build();
#endif
#if (rabbitmq)
	private readonly RabbitMqContainer _messaging = new RabbitMqBuilder("rabbitmq:4.1-management").Build();
#endif

	public async ValueTask InitializeAsync()
	{
		await _database.StartAsync(TestContext.Current.CancellationToken);
#if (redis)
		await _cache.StartAsync(TestContext.Current.CancellationToken);
#endif
#if (rabbitmq)
		await _messaging.StartAsync(TestContext.Current.CancellationToken);
#endif
		await CreateSchemaAsync(this);
	}

	/// <summary>
	/// A separate API host with its own empty database, for tests that count rows (totals, pages).
	/// The shared database can't be used for that: other tests add rows to it at the same time.
	/// </summary>
	public async Task<WebApplicationFactory<Program>> WithEmptyDatabaseAsync()
	{
		var databaseName = $"CleanSlicesTests_{Guid.NewGuid():N}";
#if (rabbitmq)
		var factory = WithWebHostBuilder(builder =>
		{
			UseDatabase(builder, databaseName);
			WithoutConsumer(builder);
		});
#else
		var factory = WithWebHostBuilder(builder => UseDatabase(builder, databaseName));
#endif
		await CreateSchemaAsync(factory);
		return factory;
	}
#if (redis)

	// A second instance of the API on the same database and cache, as behind a load balancer.
	public WebApplicationFactory<Program> AnotherInstance() =>
#if (rabbitmq)
		WithWebHostBuilder(WithoutConsumer);
#else
		WithWebHostBuilder(_ => { });
#endif
#endif

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		UseDatabase(builder, "CleanSlicesTests");
#if (redis)
		builder.UseSetting($"ConnectionStrings:{InfrastructureRegistration.CacheConnectionName}", _cache.GetConnectionString());
#endif
#if (rabbitmq)
		builder.UseSetting($"ConnectionStrings:{InfrastructureRegistration.MessagingConnectionName}", _messaging.GetConnectionString());
#endif
#if (outbox)

		// Tests process the outbox themselves, so its background loop never races them; retries are immediate.
		builder.UseSetting("Outbox:RetryDelay", "00:00:00");
#endif
#if (integration_events)
		builder.ConfigureTestServices(services =>
		{
#if (outbox)
			services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(OutboxWorker)));
#endif
#if (inbox)
			services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(InboxCleanupWorker)));
#endif
			TestEventHandlers.Add(services);
		});
#endif
	}
#if (rabbitmq)

	// Extra hosts don't consume: a message they took could be lost to the tests watching the main host,
	// or delivered again when they stop.
	private static void WithoutConsumer(IWebHostBuilder builder) =>
		builder.ConfigureTestServices(services =>
			services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(RabbitMqConsumer))));
#endif

	private void UseDatabase(IWebHostBuilder builder, string databaseName)
	{
#if (postgresql)
		var connectionString = new NpgsqlConnectionStringBuilder(_database.GetConnectionString())
		{
			Database = databaseName.ToLowerInvariant()
		};
#else
		var connectionString = new SqlConnectionStringBuilder(_database.GetConnectionString())
		{
			InitialCatalog = databaseName
		};
#endif

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
#if (redis)
		await _cache.DisposeAsync();
#endif
#if (rabbitmq)
		await _messaging.DisposeAsync();
#endif
	}
}
