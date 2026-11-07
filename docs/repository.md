# Repository / unit of work

Endpoints don't use `AppDbContext`. They load and store entities through a **repository per entity** and end the work with **`IUnitOfWork`**, both defined in Core. Infrastructure implements them with EF Core.

```csharp
public async Task<IResult> Handler(IProductRepository products, IUnitOfWork unitOfWork, Guid id, UpdateProductNameRequest request, CancellationToken cancellationToken)
{
	var product = await products.GetByIdAsync(id, cancellationToken)
		?? throw ProductErrors.NotFound(id);

	product.UpdateName(request.Name);
	await unitOfWork.SaveChangesAsync(cancellationToken);

	return Results.NoContent();
}
```

## The pieces

| Type | Where | What |
|---|---|---|
| `IRepository<T>` | `Core/Domain/IRepository.cs` | What every repository has: `GetByIdAsync`, `ExistsAsync`, `GetPageAsync` (newest first, read only), `Add`, `Remove` |
| `IUnitOfWork` | `Core/Domain/IUnitOfWork.cs` | `SaveChangesAsync`. Implemented by `AppDbContext` itself |
| `Page<T>` | `Core/Shared/Page.cs` | A page of results, with `Map` to turn entities into responses. Same JSON as the paged lists without this option |
| `Repository<T>` | `Infrastructure/Persistence/Repositories/` | The EF Core implementation of the common methods |
| `IProductRepository`, `ProductRepository` | `Core/Domain/Products/`, `Infrastructure/Persistence/Repositories/` | One pair per entity, for queries that only make sense for it. `add feature` generates them empty |

Repositories are registered by scanning: a class that implements an entity's repository interface is found and registered as that interface. A test checks that every repository interface in Core has an implementation.

## Rules

- **Repositories never save.** The handler calls `IUnitOfWork.SaveChangesAsync` once, at the end. The unit of work and the repositories share the request's `AppDbContext`, so everything changed in a request is saved together, and the save pipeline (domain events, and every option that hooks into saving) runs as before.
- **Entity-specific queries go in the entity's repository**, with names from the domain: `IsEmailInUseAsync`, `GetWithAssignmentsAsync`. Don't return `IQueryable`: the point is that the endpoint can't shape queries itself.
- **`GetByIdAsync` tracks the entity**, so it can be changed and saved. `GetPageAsync` doesn't track: it's for reading.
- Infrastructure code (domain event handlers, the save pipeline) keeps using `AppDbContext` directly. The rule is for endpoints.

## Adding a query

```csharp
public interface IProductRepository : IRepository<Product>
{
	public Task<Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}

internal sealed class ProductRepository(AppDbContext context) : Repository<Product>(context), IProductRepository
{
	public Task<Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
		Set.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
}
```

## Features written before this option

Endpoints you wrote yourself keep using `AppDbContext` until you change them: adding the option changes only the code that came from the templates. Create the repository pair by hand (as above) and replace `AppDbContext` in their handlers.

## Trade-offs

- **Reads load whole entities.** Without the option, read endpoints select only the columns of their response. Here `GetByIdAsync` and `GetPageAsync` return entities, and the endpoint maps them, so every column is loaded, and `GetByIdAsync` tracks what it loads. For most tables this doesn't matter. Where it does, add a specific query to the entity's repository, or use CQRS, whose queries read without repositories.
- **More types per entity** (an interface and a class), and a layer between the endpoint and EF Core. In return, endpoints only depend on Core, and the queries an entity supports are named in one place.
