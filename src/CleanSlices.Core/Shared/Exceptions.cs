namespace CleanSlices.Core.Shared;

public abstract class DomainException(string code, string message) : Exception(message)
{
	public string Code { get; } = code;
}

public sealed class DomainValidationException(IReadOnlyList<FieldError> errors)
	: DomainException("Validation failed", "One or more validation errors occurred.")
{
	public IReadOnlyList<FieldError> Errors { get; } = errors;

	public DomainValidationException(FieldError error) : this([error])
	{
	}

	public static void ThrowIfAny(params FieldError?[] checks)
	{
		var errors = checks.OfType<FieldError>().ToList();
		if (errors.Count > 0)
		{
			throw new DomainValidationException(errors);
		}
	}
}

public sealed class BusinessRuleException(string code, string message) : DomainException(code, message);

public sealed class NotFoundException(string code, string message) : DomainException(code, message);

public sealed class ConflictException(string code, string message) : DomainException(code, message);
