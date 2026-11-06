using CleanSlices.Api.Configuration;
#if (!result)
using CleanSlices.Core.Shared;
#endif
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CleanSlices.Api.Handlers;

public class ExceptionHandler(ILogger<ExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
#if (!result)
		if (exception is DomainException domainException)
		{
			// Expected failures (validation, not found, conflict...): not an error from the server's point of view.
			logger.LogInformation("{ExceptionType} for {Method} {Path}: {Message}",
				domainException.GetType().Name, httpContext.Request.Method, httpContext.Request.Path, domainException.Message);

			return await WriteAsync(httpContext, exception, ToProblemDetails(domainException));
		}

#endif
		if (exception is BadHttpRequestException badHttpRequestException)
		{
			logger.LogWarning(exception, "Malformed request for {Method} {Path}",
				httpContext.Request.Method, httpContext.Request.Path);

			return await WriteAsync(httpContext, exception, new ProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "Malformed request",
				Detail = badHttpRequestException.Message,
				Type = ProblemDetailsDefaults.RFC400Type
			});
		}

		logger.LogError(exception, "An unexpected error occurred for {Method} {Path}",
			httpContext.Request.Method, httpContext.Request.Path);

		return await WriteAsync(httpContext, exception, new ProblemDetails
		{
			Status = StatusCodes.Status500InternalServerError,
			Title = ProblemDetailsDefaults.RFC500Title,
			Detail = ProblemDetailsDefaults.RFC500Details,
			Type = ProblemDetailsDefaults.RFC500Type
		});
	}
#if (!result)

	private static ProblemDetails ToProblemDetails(DomainException exception)
	{
		var (status, type) = exception switch
		{
			NotFoundException => (StatusCodes.Status404NotFound, ProblemDetailsDefaults.RFC404Type),
			ConflictException => (StatusCodes.Status409Conflict, ProblemDetailsDefaults.RFC409Type),
			DomainValidationException or BusinessRuleException => (StatusCodes.Status400BadRequest, ProblemDetailsDefaults.RFC400Type),
			_ => (StatusCodes.Status400BadRequest, ProblemDetailsDefaults.RFC400Type),
		};

		var problemDetails = exception is DomainValidationException validation
			? new HttpValidationProblemDetails(validation.Errors.ToValidationErrors())
			: new ProblemDetails();

		problemDetails.Status = status;
		problemDetails.Title = exception.Code;
		problemDetails.Detail = exception.Message;
		problemDetails.Type = type;

		return problemDetails;
	}
#endif

	private ValueTask<bool> WriteAsync(HttpContext httpContext, Exception exception, ProblemDetails problemDetails)
	{
		httpContext.Response.StatusCode = problemDetails.Status!.Value;
		return problemDetailsService.TryWriteAsync(new ProblemDetailsContext
		{
			HttpContext = httpContext,
			ProblemDetails = problemDetails,
			Exception = exception
		});
	}
}
