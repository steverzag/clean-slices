# Error handling

Expected failures (invalid input, a missing entity, a conflict) are **results**: domain methods return `Result` / `Result<T>` instead of throwing, and endpoints turn failures into ProblemDetails responses. Bugs still throw and become 500s. This solution uses the `result` option.

## Domain errors

- **Field errors** (`Core/Domain/<Area>/<Entity>Errors.cs`) are `FieldError(Field, Description)` records. Each one describes one invalid value, such as an empty name or a name that is too long. `Field` is the property name (`nameof(Product.Name)`).
- **Expected failures are results** (`Core/Shared/Result.cs`). `Result` / `Result<T>` carry success or an `Error`, and support `Map`, `Bind`, `Tap`, `TapAsync`, `TapError` and `Match`. Error types (`Core/Shared/Error.cs`):
  - `ValidationError` (wraps a list of `FieldError`s; `errors.TryGetValidationError(out var error)` builds one only when a check failed)
  - `BusinessRuleError`
  - `NotFoundError`
  - `ConflictError`
- Entity factories (`Create(...)`) return `Result<T>`; methods that can fail return `Result`.

## API errors

- **API-level errors live next to the features**, not in Core: `Features/<Area>/<Entity>Errors.cs` (`NotFound`, and conflicts such as an email already in use). They depend on the database, so they are not domain rules.
- **Handlers compose results**: domain methods return a `Result`, which handlers chain with `Tap` (add, save) and `Map` (to the response record). Lookups that fail return early with `ProductErrors.NotFound(id).ToProblem()`.

## From result to response

`Extensions/ResultExtensions.cs` turns results into responses (`ToOkOrProblem`, `ToNoContentOrProblem`, `ToCreatedAtRouteOrProblem`, ...). Some of these helpers are unused at first; they're there for the endpoints you add.

| Error               | Status |
|---------------------|--------|
| `ValidationError`   | 400, with `errors` per field (see below) |
| `BusinessRuleError` | 400    |
| `NotFoundError`     | 404    |
| `ConflictError`     | 409    |
| anything else       | 500    |

Validation failures use the standard `ValidationProblemDetails` shape: `errors` maps each field, named as in the JSON body, to its messages.

```json
"errors": {
  "email": [ "Email is not a valid email address." ],
  "name": [ "Name cannot exceed 200 characters." ]
}
```

**Unhandled exceptions** go to `Handlers/ExceptionHandler.cs`, which writes responses through `IProblemDetailsService`. A `BadHttpRequestException` returns 400. Anything else is logged and returns a generic 500. Clients aren't told more than that: the log has the details.

Every ProblemDetails response includes `instance` (`METHOD /path`), `requestId` and `traceId`, and its title is the error code, such as `Product.NotFound`.

## Trade-off

Signatures show what can fail and nothing is thrown, but every caller has to handle or pass on the `Result`, and chains of `Tap`/`Map` take some getting used to.
