# Database

This solution uses **PostgreSQL**, through Npgsql.

## Where the database comes from

The connection string is called `Database`. Where the database comes from depends on how you run the API.

**With the AppHost (default): PostgreSQL in a container.** `dotnet run --project aspire/CleanSlices.AppHost` starts PostgreSQL and passes its connection string to the API.

- It listens on `127.0.0.1:15432` (the port is fixed in `AppHost.cs`), and the database is called `cleanslices`. Use `127.0.0.1` rather than `localhost`: Aspire forwards the port only on IPv4. If another solution already uses port 15432, change the port in `AppHost.cs`.
- The user is `postgres`. Its password is generated on the first run and kept in the AppHost's user secrets, as `Parameters:cleanslices-postgres-password`.
- A Docker volume keeps the data between runs.
- For a web UI, add `.WithPgAdmin()` after `AddPostgres(...)` in `AppHost.cs`; it runs pgAdmin in another container, linked from the Aspire dashboard.

**With the AppHost and an existing server.** Set the connection string in the AppHost's user secrets; the AppHost then uses that server and starts no container:

```powershell
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Database=cleanslices;Username=postgres;Password=<password>" --project aspire/CleanSlices.AppHost
```

**The API on its own** (`dotnet run --project src/CleanSlices.Api`) reads `ConnectionStrings:Database` from its `appsettings.json`, which points to a local PostgreSQL with user and password `postgres`.

## Migrations

Migrations are **never applied on startup**. You create and apply them yourself with the EF CLI; a new container starts with an empty database.

`dotnet ef` builds the DbContext through `AppDbContextFactory`. That factory reads the `ConnectionStrings__Database` environment variable, **not** appsettings. Set the variable to the database you want to change. For the container, take the password from the AppHost's user secrets (the AppHost must have run once):

```powershell
$password = (dotnet user-secrets list --project aspire/CleanSlices.AppHost |
  Select-String '^Parameters:cleanslices-postgres-password = (.+)$').Matches.Groups[1].Value
$env:ConnectionStrings__Database = "Host=127.0.0.1;Port=15432;Database=cleanslices;Username=postgres;Password=$password"

dotnet ef migrations add <Name> --project src/CleanSlices.Infrastructure
dotnet ef database update --project src/CleanSlices.Infrastructure
```

For a local server, use its connection string instead, for example the one in `appsettings.json`.

`dotnet ef` writes migrations to `src/CleanSlices.Infrastructure/Migrations`, EF's default folder. A new solution has none: the template doesn't ship migrations, because they depend on the database provider, so the first `migrations add` creates the folder. `.editorconfig` marks them as generated code, so the code-style rules don't apply to them.

## How PostgreSQL differs

**Names are snake_case.** Tables, columns, keys and indexes are lower case with underscores (`outbox_messages`, `created_at`), so SQL never needs quotes. `UseSnakeCaseNamingConvention()` handles columns and conventional names, and the model rule `Persistence/PostgreSql/SnakeCaseTableNames.cs` renames the tables set with `ToTable(...)` too. Write type configurations with ordinary names (`ToTable("OrderLines")`); they become `order_lines`.

**Text comparisons are case-sensitive.** SQL Server's default collation ignores case; PostgreSQL's doesn't. So a unique index on an email would accept both `Ana@x.com` and `ana@x.com`. The model rule `Persistence/PostgreSql/CaseInsensitiveCollation.cs` defines a collation that ignores case (but not accents), `case_insensitive`. Give it to the columns that need it, in a model rule of your own:
<!--#if (samples)-->

The sample `User.Email` does this, in `Persistence/Configuration/UserEmailCollationRule.cs`:
<!--#endif-->

```csharp
internal sealed class ProductCodeCollationRule : IModelRule
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<Product>().Property(p => p.Code).UseCollation(CaseInsensitiveCollation.Name);
}
```

PostgreSQL can't use `LIKE` on a column with that collation, so queries with `Contains`, `StartsWith` or `EndsWith` on it fail. Compare with `==` instead, or search on another column.

**Times are UTC.** Npgsql stores `DateTimeOffset` as `timestamp with time zone` and only accepts an offset of zero. The code always uses `DateTimeOffset.UtcNow`; keep it that way.

## Moving from SQL Server

When PostgreSQL was added to an existing SQL Server solution, the code was converted, but two things weren't:

- **Migrations** were generated for SQL Server. Delete the `Migrations` folder and create a first migration again.
- **Data** isn't moved. Export it from SQL Server and import it into PostgreSQL with a tool such as pgloader, or with your own scripts. Mind the new snake_case names.
