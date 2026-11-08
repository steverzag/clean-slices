using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

const string DatabaseConnectionName = "Database";
#if (postgresql)
const int PostgresPort = 15432;
#else
const int SqlServerPort = 14330;
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

builder.Build().Run();
