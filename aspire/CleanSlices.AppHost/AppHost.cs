using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

const string DatabaseConnectionName = "Database";
const int SqlServerPort = 14330;

var api = builder.AddProject<Projects.CleanSlices_Api>("cleanslices-api");

if (builder.Configuration.GetConnectionString(DatabaseConnectionName) is not null)
{
	api.WithReference(builder.AddConnectionString(DatabaseConnectionName));
}
else
{
	var database = builder.AddSqlServer("cleanslices-sql", port: SqlServerPort)
		.WithDataVolume()
		.AddDatabase(DatabaseConnectionName, databaseName: "CleanSlices");

	api.WithReference(database).WaitFor(database);
}

builder.Build().Run();
