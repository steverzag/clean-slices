#if (entra)
using CleanSlices.Api.Configuration.Authentication.Entra;
#endif
#if (identity)
using CleanSlices.Api.Configuration.Authentication.Identity;
#endif
#if (keycloak)
using CleanSlices.Api.Configuration.Authentication.Keycloak;
#endif
#if (authentication)
using CleanSlices.Api.Tests.Authentication;
#endif
using CleanSlices.Api.Tests.Infrastructure;
#if (integration_events)
using CleanSlices.Api.Tests.IntegrationEvents;
#endif
using CleanSlices.Infrastructure;
#if (identity)
using CleanSlices.Infrastructure.Identity;
#endif
#if (inbox)
using CleanSlices.Infrastructure.Inbox;
#endif
#if (servicebus)
using CleanSlices.Infrastructure.IntegrationEvents.Broker;
#endif
#if (rabbitmq)
using CleanSlices.Infrastructure.IntegrationEvents.RabbitMq;
#endif
#if (servicebus)
using CleanSlices.Infrastructure.IntegrationEvents.ServiceBus;
#endif
#if (outbox)
using CleanSlices.Infrastructure.Outbox;
#endif
using CleanSlices.Infrastructure.Persistence;
#if (identity)
using Microsoft.AspNetCore.DataProtection;
#endif
using Microsoft.AspNetCore.Hosting;
#if (identity)
using Microsoft.AspNetCore.Identity;
#endif
#if (entra)
using Microsoft.AspNetCore.Authentication.JwtBearer;
#endif
#if (integration_events || entra || identity)
using Microsoft.AspNetCore.TestHost;
#endif
using Microsoft.AspNetCore.Mvc.Testing;
#if (!postgresql)
using Microsoft.Data.SqlClient;
#endif
#if (entra)
using Microsoft.Extensions.Configuration;
#endif
using Microsoft.Extensions.DependencyInjection;
#if (entra)
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
#endif
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
#if (authentication)
using System.Net.Http.Headers;
#endif
#if (entra)
using System.Security.Cryptography;
#endif
#if (identity)
using System.Net.Http.Json;
#endif
#if (servicebus || keycloak || identity)
using System.Text.Json;
#endif
#if (rabbitmq)
using Testcontainers.RabbitMq;
#endif
#if (servicebus)
using Testcontainers.ServiceBus;
#endif
#if (keycloak)
using Testcontainers.Keycloak;
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
#elif (servicebus)
	private readonly ServiceBusContainer _messaging = new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:2.0.1")
		.WithAcceptLicenseAgreement(true)
		.WithConfig(EmulatorConfig())
		.Build();
#endif
#if (keycloak)
	private readonly KeycloakContainer _keycloak = new KeycloakBuilder("quay.io/keycloak/keycloak:26.8")
		.WithRealm(Path.Combine(AppContext.BaseDirectory, "Authentication", "realm.json"))
		.Build();
#endif
#if (entra)
	private static readonly EntraOptions TestEntra = new()
	{
		TenantId = "00000000-0000-0000-0000-00000000aaaa",
		ClientId = "00000000-0000-0000-0000-00000000bbbb",
	};
	private readonly SymmetricSecurityKey _signingKey = new(RandomNumberGenerator.GetBytes(32));
#endif
#if (identity)
	private readonly string _tokenKeys = Path.Combine(Path.GetTempPath(), $"cleanslices-tests-keys-{Guid.NewGuid():N}");
	private readonly SemaphoreSlim _logins = new(1, 1);
#endif
#if (authentication)
	private string _userToken = "";
#endif

	public async ValueTask InitializeAsync()
	{
		await _database.StartAsync(TestContext.Current.CancellationToken);
#if (redis)
		await _cache.StartAsync(TestContext.Current.CancellationToken);
#endif
#if (message_broker)
		await _messaging.StartAsync(TestContext.Current.CancellationToken);
#endif
#if (keycloak)
		await _keycloak.StartAsync(TestContext.Current.CancellationToken);
#endif
		await CreateSchemaAsync(this);
#if (authentication)
		_userToken = await LoginAsync(TestUsers.User);
#endif
	}
#if (authentication)

	// Every client the tests create is logged in as the test user.
	protected override void ConfigureClient(HttpClient client)
	{
		base.ConfigureClient(client);
		if (_userToken.Length > 0)
		{
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);
		}
	}
#endif
#if (identity)

	// Every test host, and the tests' own apps, read tokens with the same keys.
	public void ShareTokenKeys(IServiceCollection services) =>
		services.AddDataProtection()
			.SetApplicationName("CleanSlices.Api.Tests")
			.PersistKeysToFileSystem(new DirectoryInfo(_tokenKeys));

	// The test users register and log in through the API's own endpoints; the admin gets the role directly.
	public async Task<string> LoginAsync(string username)
	{
		await _logins.WaitAsync(TestContext.Current.CancellationToken);
		try
		{
			var email = $"{username}@example.com";
			var password = $"{username}-Passw0rd!";
			using var client = CreateClient();
			client.DefaultRequestHeaders.Authorization = null;

			// 400 once the user exists, from an earlier login in this run.
			await client.PostAsJsonAsync($"{IdentityAuthentication.RoutePrefix}/register", new { email, password }, TestContext.Current.CancellationToken);
			if (username == TestUsers.Admin)
			{
				await AddToAdminsAsync(email);
			}

			using var response = await client.PostAsJsonAsync($"{IdentityAuthentication.RoutePrefix}/login", new { email, password }, TestContext.Current.CancellationToken);
			response.EnsureSuccessStatusCode();

			using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
			return json.RootElement.GetProperty("accessToken").GetString()!;
		}
		finally
		{
			_logins.Release();
		}
	}

	public async Task<string> UserIdAsync(string username)
	{
		using var scope = Services.CreateScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
		var user = await users.FindByEmailAsync($"{username}@example.com");
		return user?.Id ?? throw new InvalidOperationException($"The test user {username} isn't registered.");
	}

	private async Task AddToAdminsAsync(string email)
	{
		using var scope = Services.CreateScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
		var user = await users.FindByEmailAsync(email)
			?? throw new InvalidOperationException($"{email} wasn't registered.");
		if (!await users.IsInRoleAsync(user, AppRoles.Admin))
		{
			var result = await users.AddToRoleAsync(user, AppRoles.Admin);
			if (!result.Succeeded)
			{
				throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
			}
		}
	}
#endif
#if (entra)

	// The tests can't log in to a tenant: they sign their own tokens, and the API trusts only that key and issuer.
	// Everything else is the real validation: issuer, audience, roles, and the object ID as the user ID.
	public void UseEntraSettings(IConfiguration configuration)
	{
		configuration[$"{EntraOptions.Section}:TenantId"] = TestEntra.TenantId;
		configuration[$"{EntraOptions.Section}:ClientId"] = TestEntra.ClientId;
	}

	public void TrustTestTokens(IServiceCollection services) =>
		services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
		{
			var configuration = new OpenIdConnectConfiguration { Issuer = TestEntra.Authority };
			configuration.SigningKeys.Add(_signingKey);
			options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
		});

	public Task<string> LoginAsync(string username)
	{
		var claims = new Dictionary<string, object>
		{
			["sub"] = $"subject-of-{username}",
			["oid"] = ObjectId(username),
			["name"] = username,
		};
		if (username == TestUsers.Admin)
		{
			claims["roles"] = new[] { TestUsers.AdminRole };
		}

		var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
		{
			Issuer = TestEntra.Authority,
			Audience = TestEntra.ClientId,
			Claims = claims,
			Expires = DateTime.UtcNow.AddHours(1),
			SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
		});

		return Task.FromResult(token);
	}

	public static string ObjectId(string username) =>
		new Guid(MD5.HashData(System.Text.Encoding.UTF8.GetBytes(username))).ToString();
#endif
#if (keycloak)

	public string KeycloakAuthority =>
		_keycloak.GetBaseAddress().TrimEnd('/') + new Uri(new KeycloakOptions().Authority).AbsolutePath;

	// The realm's test users have their name as password; the client allows the password grant for this.
	public async Task<string> LoginAsync(string username)
	{
		using var http = new HttpClient();
		using var response = await http.PostAsync(
			$"{KeycloakAuthority}/protocol/openid-connect/token",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "password",
				["client_id"] = new KeycloakOptions().Audience,
				["username"] = username,
				["password"] = username,
				["scope"] = "openid",
			}),
			TestContext.Current.CancellationToken);
		response.EnsureSuccessStatusCode();

		using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
		return json.RootElement.GetProperty("access_token").GetString()!;
	}
#endif

	/// <summary>
	/// A separate API host with its own empty database, for tests that count rows (totals, pages).
	/// The shared database can't be used for that: other tests add rows to it at the same time.
	/// </summary>
	public async Task<WebApplicationFactory<Program>> WithEmptyDatabaseAsync()
	{
		var databaseName = $"CleanSlicesTests_{Guid.NewGuid():N}";
#if (message_broker)
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
#if (message_broker)
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
#if (message_broker)
		builder.UseSetting($"ConnectionStrings:{InfrastructureRegistration.MessagingConnectionName}", _messaging.GetConnectionString());
#endif
#if (keycloak)
		builder.UseSetting($"{KeycloakOptions.Section}:Authority", KeycloakAuthority);
#elif (entra)
		builder.UseSetting($"{EntraOptions.Section}:TenantId", TestEntra.TenantId);
		builder.UseSetting($"{EntraOptions.Section}:ClientId", TestEntra.ClientId);
		builder.ConfigureTestServices(TrustTestTokens);
#elif (identity)
		builder.ConfigureTestServices(ShareTokenKeys);
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
#if (message_broker)

	// Extra hosts don't consume: a message they took could be lost to the tests watching the main host,
	// or delivered again when they stop.
#if (rabbitmq)
	private static void WithoutConsumer(IWebHostBuilder builder) =>
		builder.ConfigureTestServices(services =>
			services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(RabbitMqConsumer))));
#else
	private static void WithoutConsumer(IWebHostBuilder builder) =>
		builder.ConfigureTestServices(services =>
			services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ServiceBusConsumer))));
#endif
#endif
#if (servicebus)

	// The emulator only knows the entities in its config: the app's topic and subscription, with the app's names.
	private static string EmulatorConfig()
	{
		var settings = new BrokerOptions();
		var subscription = new
		{
			Name = settings.Subscription,
			Properties = new
			{
				DeadLetteringOnMessageExpiration = false,
				DefaultMessageTimeToLive = "PT1H",
				LockDuration = "PT1M",
				MaxDeliveryCount = settings.MaxDeliveries,
				ForwardDeadLetteredMessagesTo = "",
				ForwardTo = "",
				RequiresSession = false,
			},
			Rules = Array.Empty<object>(),
		};
		var topic = new
		{
			Name = settings.Topic,
			Properties = new { DefaultMessageTimeToLive = "PT1H", DuplicateDetectionHistoryTimeWindow = "PT20S", RequiresDuplicateDetection = false },
			Subscriptions = new[] { subscription },
		};
		var config = new
		{
			UserConfig = new
			{
				Namespaces = new[] { new { Name = "sbemulatorns", Queues = Array.Empty<object>(), Topics = new[] { topic } } },
				Logging = new { Type = "File" },
			},
		};

		var path = Path.Combine(Path.GetTempPath(), $"servicebus-emulator-{Guid.NewGuid():N}.json");
		File.WriteAllText(path, JsonSerializer.Serialize(config));
		return path;
	}
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
#if (message_broker)
		await _messaging.DisposeAsync();
#endif
#if (keycloak)
		await _keycloak.DisposeAsync();
#endif
	}
}
