# Error handling

Expected failures (invalid input, a missing entity, a conflict) are **exceptions**. They become ProblemDetails responses in one place, so endpoints never catch them.

## Domain errors

- **Field errors** (`Core/Domain/<Area>/<Entity>Errors.cs`) are `FieldError(Field, Description)` records. Each one describes one invalid value, such as an empty name or a name that is too long. `Field` is the property name (`nameof(Product.Name)`).
- **Expected failures are exceptions** (`Core/Shared/Exceptions.cs`). All of them derive from `DomainException`, which carries a `Code`:
  - `DomainValidationException` (wraps a list of `FieldError`s; `DomainValidationException.ThrowIfAny(...)` throws only when a check failed)
  - `BusinessRuleException`
  - `NotFoundException`
  - `ConflictException`
- Entity factories (`Create(...)`) and methods validate their input and throw a `DomainValidationException` when it's invalid.

## API errors

- **API-level errors live next to the features**, not in Core: `Features/<Area>/<Entity>Errors.cs` (`NotFound`, and conflicts such as an email already in use). They depend on the database, so they are not domain rules.
- **Handlers are plain imperative code**: load the entity, call the domain method, save, return `Results.*`. Failures are thrown (`throw ProductErrors.NotFound(id)`) and never caught in the endpoint.

## From exception to response

`Handlers/ExceptionHandler.cs` writes ProblemDetails through `IProblemDetailsService`:

| Exception                   | Status |
|-----------------------------|--------|
| `DomainValidationException` | 400, with `errors` per field (see below) |
| `BusinessRuleException`     | 400    |
| `NotFoundException`         | 404    |
| `ConflictException`         | 409    |
| `BadHttpRequestException`   | 400    |
| anything else               | 500    |

Domain exceptions are expected, so they're logged at Information level. Anything else is logged as an error and returns a generic 500. Clients aren't told more than that: the log has the details.

Validation failures use the standard `ValidationProblemDetails` shape: `errors` maps each field, named as in the JSON body, to its messages.

```json
"errors": {
  "email": [ "Email is not a valid email address." ],
  "name": [ "Name cannot exceed 200 characters." ]
}
```

Every ProblemDetails response includes `instance` (`METHOD /path`), `requestId` and `traceId`, and its title is the error code, such as `Product.NotFound`.

## Trade-off

The code reads top to bottom, but method signatures don't show what can fail, and throwing costs more than returning. The `result` option is the alternative: domain methods return `Result` / `Result<T>` instead.
