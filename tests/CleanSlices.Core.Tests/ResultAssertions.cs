#if (result)
using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Tests;

public static class ResultAssertions
{
	extension(Result result)
	{
		/// <summary>Asserts the result failed with a validation error and returns its field errors.</summary>
		public IReadOnlyList<FieldError> ShouldFailValidation()
		{
			Assert.True(result.IsFailure, "Expected a failed result, but it succeeded.");
			return Assert.IsType<ValidationError>(result.Error).Errors;
		}

		public void ShouldSucceed()
		{
			Assert.True(result.IsSuccess, $"Expected success, but it failed with: {result.Error}");
		}
	}

	extension<T>(Result<T> result)
	{
		/// <summary>Asserts the result succeeded and returns its value.</summary>
		public T ShouldSucceedWithValue()
		{
			Assert.True(result.IsSuccess, $"Expected success, but it failed with: {result.Error}");
			return result.Value;
		}
	}
}
#endif
