using CleanSlices.Core.Shared;

namespace CleanSlices.Api.Configuration;

public static class FieldErrorExtensions
{
	extension(IEnumerable<FieldError> errors)
	{
		// The ValidationProblemDetails "errors" shape: field -> its messages.
		// Field names stay as C# names here; ProblemDetailsCustomization converts them to JSON names.
		public Dictionary<string, string[]> ToValidationErrors() =>
			errors
				.GroupBy(error => error.Field)
				.ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
	}
}
