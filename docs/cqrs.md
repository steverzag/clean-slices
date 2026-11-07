# CQRS

Each endpoint turns its request into a **command** (it changes something) or a **query** (it reads), and sends it to the **handler** that does the work. Commands and queries use the same database; there is no separate read store.

Everything for one endpoint stays in its slice file: the request, the command or query, its handler and the endpoint.

```csharp
public class UpdateProductName : IEndpoint
{
	public record UpdateProductNameRequest(string Name);

	public sealed record UpdateProductNameCommand(Guid Id, string Name) : ICommand;

	internal sealed class UpdateProductNameHandler(AppDbContext context) : ICommandHandler<UpdateProductNameCommand>
	{
		public async ValueTask<Unit> Handle(UpdateProductNameCommand command, CancellationToken cancellationToken)
		{
			var entity = await context.Set<Product>().FindAsync([command.Id], cancellationToken)
				?? throw ProductErrors.NotFound(command.Id);

			entity.UpdateName(command.Name);
			await context.SaveChangesAsync(cancellationToken);
			return Unit.Value;
		}
	}

	public async Task<IResult> Handler(ISender sender, Guid id, UpdateProductNameRequest request, CancellationToken cancellationToken)
	{
		await sender.Send(new UpdateProductNameCommand(id, request.Name), cancellationToken);
		return Results.NoContent();
	}
}
```

## The pieces

| Type | What |
|---|---|
| `ICommand`, `ICommand<TResponse>` | A change. `ICommand` returns nothing (`Unit`) |
| `IQuery<TResponse>` | A read |
| `ICommandHandler<TCommand>`, `ICommandHandler<TCommand, TResponse>`, `IQueryHandler<TQuery, TResponse>` | Do the work. Registered automatically |
| `ISender` | `Send(command)` / `Send(query)` finds the handler and calls it. Endpoints only take `ISender` |
| `IReadDb` | What queries read from: `Query<T>()`, never tracked, so a query can't change and save anything |

<!--#if (mediator)-->
The dispatcher is the [Mediator](https://github.com/martinothamar/Mediator) package (MIT). Its source generator finds the handlers at build time; `Configuration/Cqrs/CqrsRegistration.cs` registers them with a scoped lifetime, because handlers use the request's database context.

Mediator also has `INotification`, `INotificationHandler<T>` and `IPublisher`. Domain events don't use them: they use the solution's own interfaces of the same names, in `Core/Messages`, and are dispatched by the save pipeline as before. The two sets never meet in generated code (Mediator's `global using` is only in the Api, which doesn't use the domain-event interfaces). If an Api file imports `Core.Messages`, the compiler reports the names as ambiguous (`CS0104`); write the full name, or add an alias such as `using DomainPublisher = CleanSlices.Core.Messages.IPublisher;`.
<!--#else-->
The dispatcher is a small one of its own, in `Configuration/Cqrs/` (`Messages.cs`, `Sender.cs`): no package. Handlers are found by scanning the Api assembly. It uses the names and shapes of the [Mediator](https://github.com/martinothamar/Mediator) package, so switching to it later changes the package and one `global using` line, not the slices.
<!--#endif-->

`Configuration/Cqrs/CqrsUsings.cs` makes the interfaces available everywhere with one `global using`, so slices don't name the dispatcher.

## Rules

- **Commands return nothing, or what the endpoint answers.** A create returns its response (`CreateProductResponse`, with the new ID), so the endpoint doesn't need a second read. Other commands return `Unit`.
<!--#if (result)-->
- **Handlers return results.** A command returns `Result` (or `Result<CreateProductResponse>`), a query `Result<TResponse>`; failures are `Result.Failure(...)`, and the endpoint turns the result into the response, as without CQRS.
<!--#else-->
- **Handlers throw the same exceptions as before** (`ProductErrors.NotFound`), and the exception handler turns them into responses, as without CQRS.
<!--#endif-->
<!--#if (repository)-->
- **Commands use repositories and `IUnitOfWork`; queries use `IReadDb`** and keep their `Select(...)` projections, so reads load only the columns they return.
<!--#else-->
- **Commands use `AppDbContext`; queries use `IReadDb`** and keep their `Select(...)` projections.
<!--#endif-->
- **Request validation, idempotency and every other endpoint concern stay at the endpoint**, not in a pipeline around the handlers: one place for them, and a handler can be called from anywhere with the same result.
- Domain events and everything that hooks into saving work as before: handlers save the same way endpoints did.

## Trade-offs

- **More types per endpoint** (a command or query and a handler), and one indirection between the endpoint and the code that runs. In return, endpoints only translate HTTP, and each handler has one job.
<!--#if (mediator)-->
- **A missing handler fails the build**: Mediator's source generator knows every message and handler, and reports a message without one (`MSG0005`), which this solution treats as an error.
- No pipeline behaviors (decorators around every handler) are set up. If you need one, for logging or transactions, Mediator has `IPipelineBehavior`.
<!--#else-->
- **A missing handler fails at run time**, when its command is sent ("No service for type 'ICommandHandler<…>'"), not when the solution builds. Every endpoint test sends its command, so the tests catch it.
- No pipeline behaviors (decorators around every handler) are set up. If you need one, for logging or transactions, wrap the call in `Sender`.
<!--#endif-->
