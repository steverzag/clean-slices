# Request validation

Requests are validated with [FluentValidation](https://docs.fluentvalidation.net) before the handler runs. A validator checks the **shape** of a request: fields that are missing, empty, too long, or not what they claim to be. **The domain stays the authority**: entities still validate everything they're given, wherever it comes from, and business rules belong there, not in validators.

## Adding a validator

Put the validator next to the request, in the slice file:

```csharp
public record CreateProductRequest(string Name, string Code);

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
	public CreateProductRequestValidator()
	{
		RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
			.NotEmpty().WithMessage(ProductFieldErrors.EmptyName.Description)
			.MaximumLength(Product.NameMaxLength).WithMessage(ProductFieldErrors.NameTooLong.Description);
	}
}
```

That's all: validators are found by scanning, and **every endpoint validates its arguments automatically** when they have a validator (`Configuration/Validation/RequestValidation.cs`, added to all endpoints in `EndpointRegister`). Endpoints with nothing to validate get no filter at all.

- **Reuse the domain's limits and messages** (`Product.NameMaxLength`, the field errors in `Core/Domain/<Area>/<Entity>Errors.cs`, imported with an alias such as `using ProductFieldErrors = …ProductErrors;`). Each number and each message is then defined once, and a request rejected by its validator gets exactly the response the domain would have given.
- `Cascade(CascadeMode.Stop)` reports the first problem per field, like the domain does.

## What clients see

The same response as the domain's own validation: status 400, title `Validation failed`, and `errors` with each field, named as in the JSON body, and its messages.

```json
"errors": {
  "name": [ "Name cannot be empty." ]
}
```

Validation runs before the handler, so a request that is both invalid and, say, a duplicate gets the 400 for the invalid field, not the 409.

## Trade-offs

- For simple CRUD on one entity, a validator mostly repeats what the domain checks. Its value grows with requests that don't map one to one onto an entity: several entities, optional parts, formats.
- Validation always runs at the endpoint. If you're used to MediatR, you may expect a `ValidationBehavior` in a pipeline; this solution doesn't use one, on purpose: one place, and the domain still guards every command wherever it comes from.
