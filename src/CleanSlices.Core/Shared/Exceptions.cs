namespace CleanSlices.Core.Shared;

public sealed record DomainError(string Code, string Description);

public abstract class DomainException(string code, string message) : Exception(message)
{
	public string Code { get; } = code;
}

public sealed class DomainValidationException(IReadOnlyList<DomainError> errors)
	: DomainException("Validation failed", "One or more validation errors occurred.")
{
	public IReadOnlyList<DomainError> Errors { get; } = errors;

	public DomainValidationException(DomainError error) : this([error])
	{
	}

	public static void ThrowIfAny(params DomainError?[] checks)
	{
		var errors = checks.OfType<DomainError>().ToList();
		if (errors.Count > 0)
		{
			throw new DomainValidationException(errors);
		}
	}
}

public sealed class BusinessRuleException(string code, string message) : DomainException(code, message);

public sealed class NotFoundException(string code, string message) : DomainException(code, message);

public sealed class ConflictException(string code, string message) : DomainException(code, message);
