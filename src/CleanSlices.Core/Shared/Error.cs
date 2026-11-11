#if (result)
using System.Diagnostics.CodeAnalysis;

namespace CleanSlices.Core.Shared;

public record Error(string Code, string Description);

public record BusinessRuleError(string Code, string Description) : Error(Code, Description);
public record NotFoundError(string Code, string Description) : Error(Code, Description);
public record ConflictError(string Code, string Description) : Error(Code, Description);
public record ValidationError(string Code, string Description, IReadOnlyList<FieldError> Errors) : Error(Code, Description);

public static class ErrorExtensions
{
	extension(List<FieldError> errors)
	{
		public bool TryGetValidationError([NotNullWhen(true)] out ValidationError? validationError, string code = "Validation failed", string description = "One or more validation errors occurred.")
		{
			if (errors is [_, ..])
			{
				validationError = new ValidationError(code, description, errors);
				return true;
			}
			validationError = null;
			return false;
		}
	}
}
#endif
