using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

const string DatabaseConnectionName = "Database";
#if (postgresql)
const int PostgresPort = 15432;
#else
const int SqlServerPort = 14330;
#endif
#if (keycloak)
const int KeycloakPort = 18080;
#endif

var api = builder.AddProject<Projects.CleanSlices_Api>("cleanslices-api");

if (builder.Configuration.GetConnectionString(DatabaseConnectionName) is not null)
{
	api.WithReference(builder.AddConnectionString(DatabaseConnectionName));
}
else
{
#if (postgresql)
	var database = builder.AddPostgres("cleanslices-postgres", port: PostgresPort)
		.WithDataVolume()
		.AddDatabase(DatabaseConnectionName, databaseName: "cleanslices");
#else
	var database = builder.AddSqlServer("cleanslices-sql", port: SqlServerPort)
		.WithDataVolume()
		.AddDatabase(DatabaseConnectionName, databaseName: "CleanSlices");
#endif

	api.WithReference(database).WaitFor(database);
}
#if (redis)

var cache = builder.AddRedis("cache");
api.WithReference(cache).WaitFor(cache);
#endif
#if (rabbitmq)

var messaging = builder.AddRabbitMQ("messaging").WithManagementPlugin();
api.WithReference(messaging).WaitFor(messaging);
#endif
#if (servicebus)

// The emulator can't create entities at run time, so the topic and subscription are declared here.
// Their names and MaxDeliveryCount match the API's Messaging settings (Topic, Subscription, MaxDeliveries).
var messaging = builder.AddAzureServiceBus("messaging").RunAsEmulator();
messaging.AddServiceBusTopic("cleanslices-events")
	.AddServiceBusSubscription("cleanslices-api-subscription", subscriptionName: "cleanslices-api")
	.WithProperties(subscription => subscription.MaxDeliveryCount = 5);
api.WithReference(messaging).WaitFor(messaging);
#endif
#if (keycloak)

// A plain container: Aspire's Keycloak integration is still in preview.
// The realm in Keycloak/ (client, roles, test users) is imported on every start; nothing else is kept.
var keycloakPassword = builder.AddParameter("cleanslices-keycloak-password", new GenerateParameterDefault { MinLength = 16 }, secret: true, persist: true);
var keycloak = builder.AddContainer("cleanslices-keycloak", "quay.io/keycloak/keycloak", "26.8")
	.WithArgs("start-dev", "--import-realm")
	.WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
	.WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", keycloakPassword)
	.WithEnvironment("KC_HEALTH_ENABLED", "true")
	.WithBindMount("Keycloak", "/opt/keycloak/data/import", isReadOnly: true)
	.WithHttpEndpoint(port: KeycloakPort, targetPort: 8080)
	.WithHttpEndpoint(targetPort: 9000, name: "management")
	.WithHttpHealthCheck("/health/ready", endpointName: "management");
api.WaitFor(keycloak);
#endif
#if (entra)

// Entra ID runs in Azure: on the first run the dashboard asks for your app registration's IDs and keeps them in user secrets.
var entraTenantId = builder.AddParameter("cleanslices-entra-tenant-id");
var entraClientId = builder.AddParameter("cleanslices-entra-client-id");
api.WithEnvironment("Authentication__Entra__TenantId", entraTenantId)
	.WithEnvironment("Authentication__Entra__ClientId", entraClientId);
#endif

builder.Build().Run();
