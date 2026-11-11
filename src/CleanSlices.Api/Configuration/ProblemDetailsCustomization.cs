using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace CleanSlices.Api.Configuration;

public static class ProblemDetailsCustomization
{
	public static void Apply(ProblemDetailsContext context)
	{
		var httpContext = context.HttpContext;

		context.ProblemDetails.Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}";
		context.ProblemDetails.Extensions.TryAdd("requestId", httpContext.TraceIdentifier);
		var activity = httpContext.Features.Get<IHttpActivityFeature>()?.Activity;
		context.ProblemDetails.Extensions.TryAdd("traceId", activity?.Id);

		if (context.ProblemDetails is HttpValidationProblemDetails validation)
		{
			UseJsonFieldNames(validation, httpContext);
		}
	}

	private static void UseJsonFieldNames(HttpValidationProblemDetails validation, HttpContext httpContext)
	{
		var namingPolicy = httpContext.RequestServices
			.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions.PropertyNamingPolicy;

		if (namingPolicy is null)
		{
			return;
		}

		validation.Errors = validation.Errors
			.GroupBy(error => string.Join('.', error.Key.Split('.').Select(namingPolicy.ConvertName)))
			.ToDictionary(group => group.Key, group => group.SelectMany(error => error.Value).ToArray());
	}
}
