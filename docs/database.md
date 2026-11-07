# Database

This solution uses **SQL Server**.

## Where the database comes from

The connection string is called `Database`. Where the database comes from depends on how you run the API.

**With the AppHost (default): SQL Server in a container.** `dotnet run --project aspire/CleanSlices.AppHost` starts SQL Server and passes its connection string to the API.

- It listens on `127.0.0.1,14330` (the port is fixed in `AppHost.cs`), and the database is called `CleanSlices`. Use `127.0.0.1`, not `localhost`: Aspire forwards the port only on IPv4, and `localhost` makes the SQL Server client try IPv6 first and fail. If another solution already uses port 14330, change the port in `AppHost.cs`.
- The `sa` password is generated on the first run and kept in the AppHost's user secrets, as `Parameters:cleanslices-sql-password`.
- A Docker volume keeps the data between runs.

**With the AppHost and an existing server.** Set the connection string in the AppHost's user secrets; the AppHost then uses that server and starts no container:

```powershell
dotnet user-secrets set "ConnectionStrings:Database" "Server=localhost;Database=CleanSlices;Trusted_Connection=True;TrustServerCertificate=True" --project aspire/CleanSlices.AppHost
```

**The API on its own** (`dotnet run --project src/CleanSlices.Api`) reads `ConnectionStrings:Database` from its `appsettings.json`, which points to a local SQL Server with Windows authentication.

## Migrations

Migrations are **never applied on startup**. You create and apply them yourself with the EF CLI; a new container starts with an empty database.

`dotnet ef` builds the DbContext through `AppDbContextFactory`. That factory reads the `ConnectionStrings__Database` environment variable, **not** appsettings. Set the variable to the database you want to change. For the container, take the password from the AppHost's user secrets (the AppHost must have run once):

```powershell
$password = (dotnet user-secrets list --project aspire/CleanSlices.AppHost |
  Select-String '^Parameters:cleanslices-sql-password = (.+)$').Matches.Groups[1].Value
$env:ConnectionStrings__Database = "Server=127.0.0.1,14330;Database=CleanSlices;User ID=sa;Password=$password;TrustServerCertificate=True"

dotnet ef migrations add <Name> --project src/CleanSlices.Infrastructure
dotnet ef database update --project src/CleanSlices.Infrastructure
```

For a local server, use its connection string instead, for example the one in `appsettings.json`.

`dotnet ef` writes migrations to `src/CleanSlices.Infrastructure/Migrations`, EF's default folder. A new solution has none: the template doesn't ship migrations, because they depend on the database provider, so the first `migrations add` creates the folder. `.editorconfig` marks them as generated code, so the code-style rules don't apply to them.

