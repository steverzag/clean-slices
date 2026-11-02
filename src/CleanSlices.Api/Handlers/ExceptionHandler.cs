using CleanSlices.Api.Configuration;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CleanSlices.Api.Handlers;

public class ExceptionHandler(ILogger<ExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
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
