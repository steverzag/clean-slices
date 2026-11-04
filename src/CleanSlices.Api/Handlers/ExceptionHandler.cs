using CleanSlices.Api.Configuration;
using CleanSlices.Core.Shared;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CleanSlices.Api.Handlers;

public class ExceptionHandler(
	ILogger<ExceptionHandler> logger,
	IProblemDetailsService problemDetailsService,
	IOptions<HttpJsonOptions> jsonOptions) : IExceptionHandler
{
	private readonly JsonNamingPolicy? _namingPolicy = jsonOptions.Value.SerializerOptions.PropertyNamingPolicy;

	public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
		if (exception is DomainException domainException)
		{
			// Expected failures (validation, not found, conflict...): not an error from the server's point of view.
			logger.LogInformation("{ExceptionType} for {Method} {Path}: {Message}",
				domainException.GetType().Name, httpContext.Request.Method, httpContext.Request.Path, domainException.Message);

			return await WriteAsync(httpContext, exception, ToProblemDetails(domainException));
		}

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

	private ProblemDetails ToProblemDetails(DomainException exception)
	{
		var (status, type) = exception switch
		{
			NotFoundException => (StatusCodes.Status404NotFound, ProblemDetailsDefaults.RFC404Type),
			ConflictException => (StatusCodes.Status409Conflict, ProblemDetailsDefaults.RFC409Type),
			DomainValidationException or BusinessRuleException => (StatusCodes.Status400BadRequest, ProblemDetailsDefaults.RFC400Type),
			_ => (StatusCodes.Status400BadRequest, ProblemDetailsDefaults.RFC400Type),
		};

		var problemDetails = exception is DomainValidationException validation
			? new HttpValidationProblemDetails(ToErrorDictionary(validation.Errors))
			: new ProblemDetails();

		problemDetails.Status = status;
		problemDetails.Title = exception.Code;
		problemDetails.Detail = exception.Message;
		problemDetails.Type = type;

		return problemDetails;
	}

	private Dictionary<string, string[]> ToErrorDictionary(IReadOnlyList<FieldError> errors) =>
		errors
			.GroupBy(error => _namingPolicy?.ConvertName(error.Field) ?? error.Field)
			.ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());

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
