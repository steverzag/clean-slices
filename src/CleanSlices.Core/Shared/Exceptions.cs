namespace CleanSlices.Core.Shared;

/// <summary>A single failure detail (e.g. one invalid field), used by the per-entity error catalogs.</summary>
public sealed record DomainError(string Code, string Description);

/// <summary>
/// Base type for expected failures. The API's exception handler translates these into ProblemDetails;
/// anything else is treated as an unexpected 500.
/// </summary>
public abstract class DomainException(string code, string message) : Exception(message)
{
	public string Code { get; } = code;
}

/// <summary>One or more input values are invalid (400, with an <c>errors</c> list).</summary>
public sealed class DomainValidationException(IReadOnlyList<DomainError> errors)
	: DomainException("Validation failed", "One or more validation errors occurred.")
{
	public IReadOnlyList<DomainError> Errors { get; } = errors;

	public DomainValidationException(DomainError error) : this([error])
	{
	}

	/// <summary>Throws when any of the given validation checks produced an error.</summary>
	public static void ThrowIfAny(params DomainError?[] checks)
	{
		var errors = checks.OfType<DomainError>().ToList();
		if (errors.Count > 0)
		{
			throw new DomainValidationException(errors);
		}
	}
}

/// <summary>A business rule was violated (400).</summary>
public sealed class BusinessRuleException(string code, string message) : DomainException(code, message);

/// <summary>The requested resource doesn't exist (404).</summary>
public sealed class NotFoundException(string code, string message) : DomainException(code, message);

/// <summary>The request conflicts with the current state, e.g. a duplicate (409).</summary>
public sealed class ConflictException(string code, string message) : DomainException(code, message);
