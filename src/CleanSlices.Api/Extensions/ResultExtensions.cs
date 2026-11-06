#if (result)
using CleanSlices.Api.Configuration;
using CleanSlices.Core.Shared;

namespace CleanSlices.Api.Extensions;

public static class ResultExtensions
{
	extension(Result source)
	{
		public IResult ToAcceptedOrProblem() =>
			source.Match(
				onSuccess: () => Results.Accepted(),
				onFailure: error => error.ToProblem());

		public IResult ToOkOrProblem() =>
			source.Match(
				onSuccess: () => Results.Ok(),
				onFailure: error => error.ToProblem());

		public IResult ToCreatedOrProblem() =>
			source.Match(
				onSuccess: () => Results.Created(),
				onFailure: error => error.ToProblem());

		public IResult ToCreatedAtRouteOrProblem(string? routeName = null, object? routeValues = null) =>
			source.Match(
				onSuccess: () => Results.CreatedAtRoute(routeName, routeValues),
				onFailure: error => error.ToProblem());

		public IResult ToNoContentOrProblem() =>
			source.Match(
				onSuccess: () => Results.NoContent(),
				onFailure: error => error.ToProblem());
	}

	extension<TIn>(Result<TIn> source)
	{
		public IResult ToAcceptedOrProblem() =>
			source.Match(
				onSuccess: (value) => Results.Accepted(value: value),
				onFailure: error => error.ToProblem());

		public IResult ToOkOrProblem() =>
			source.Match(
				onSuccess: value => Results.Ok(value),
				onFailure: error => error.ToProblem());

		public IResult ToCreatedOrProblem() =>
			source.Match(
				onSuccess: value => Results.Created(uri: (string?)default, value: value),
				onFailure: error => error.ToProblem());

		public IResult ToCreatedAtRouteOrProblem(string routeName, Func<TIn, object> routeValues) =>
			source.Match(
				onSuccess: value => Results.CreatedAtRoute(routeName, routeValues(value), value),
				onFailure: error => error.ToProblem());

		public IResult ToNoContentOrProblem() =>
			source.Match(
				onSuccess: _ => Results.NoContent(),
				onFailure: error => error.ToProblem());
	}

	extension(Error error)
	{
		public IResult ToProblem() =>
			error is ValidationError validation
				? Results.ValidationProblem
					(
						validation.Errors.ToValidationErrors(),
						title: validation.Code,
						detail: validation.Description,
						statusCode: StatusCodes.Status400BadRequest,
						type: ProblemDetailsDefaults.RFC400Type
					)
				: Results.Problem
					(
						title: error.Code ?? ProblemDetailsDefaults.RFC500Title,
						detail: error.Description ?? ProblemDetailsDefaults.RFC500Details,
						statusCode: GetStatusCode(error),
						type: GetType(error)
					);
	}

	private static string GetType(Error error)
	{
		var type = error switch
		{
			NotFoundError => ProblemDetailsDefaults.RFC404Type,
			ConflictError => ProblemDetailsDefaults.RFC409Type,
			BusinessRuleError => ProblemDetailsDefaults.RFC400Type,
			ValidationError => ProblemDetailsDefaults.RFC400Type,
			_ => ProblemDetailsDefaults.RFC500Type
		};

		return type;
	}

	private static int GetStatusCode(Error error)
	{
		var statusCode = error switch
		{
			NotFoundError => StatusCodes.Status404NotFound,
			ConflictError => StatusCodes.Status409Conflict,
			BusinessRuleError => StatusCodes.Status400BadRequest,
			ValidationError => StatusCodes.Status400BadRequest,
			_ => StatusCodes.Status500InternalServerError
		};

		return statusCode;
	}
}

#endif
