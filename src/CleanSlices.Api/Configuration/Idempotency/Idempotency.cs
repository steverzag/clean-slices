#if (idempotency)
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CleanSlices.Infrastructure.CurrentUser;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CleanSlices.Api.Configuration.Idempotency;

public static class Idempotency
{
	public const string Header = "Idempotency-Key";
	public const string ReplayedHeader = "Idempotent-Replayed";
	public const int MaxKeyLength = 255;

	private static readonly HashSet<Type> NotPartOfTheRequest =
		[typeof(CancellationToken), typeof(HttpContext), typeof(HttpRequest), typeof(HttpResponse), typeof(ClaimsPrincipal)];

	extension(IServiceCollection services)
	{
		public IServiceCollection AddIdempotency()
		{
			services.AddOptions<IdempotencyOptions>().BindConfiguration(IdempotencyOptions.Section);
			services.TryAddSingleton<KeysInProgress>();

			return services;
		}
	}

	extension(RouteHandlerBuilder builder)
	{
		public RouteHandlerBuilder WithIdempotency() => builder.AddEndpointFilterFactory(Factory);
	}

	private static EndpointFilterDelegate Factory(EndpointFilterFactoryContext context, EndpointFilterDelegate next)
	{
		// The fingerprint covers what the client sent: route, query and body values, not injected services.
		var registered = context.ApplicationServices.GetRequiredService<IServiceProviderIsService>();
		var requestArguments = context.MethodInfo.GetParameters()
			.Select((parameter, index) => (parameter.ParameterType, Index: index))
			.Where(p => !NotPartOfTheRequest.Contains(p.ParameterType) && !registered.IsService(p.ParameterType))
			.Select(p => p.Index)
			.ToArray();

		return invocation => InvokeAsync(invocation, next, requestArguments);
	}

	private static async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next, int[] requestArguments)
	{
		var httpContext = invocation.HttpContext;
		var services = httpContext.RequestServices;
		var options = services.GetRequiredService<IOptions<IdempotencyOptions>>().Value;

		var headers = httpContext.Request.Headers[Header];
		if (headers.Count == 0)
		{
			return options.Required
				? Problem(StatusCodes.Status400BadRequest, "Idempotency key required", $"This endpoint requires an {Header} header.")
				: await next(invocation);
		}

		var key = headers.Count == 1 ? headers[0]?.Trim('"') : null;
		if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength)
		{
			return Problem(StatusCodes.Status400BadRequest, "Invalid idempotency key",
				$"The {Header} header must be a single value of at most {MaxKeyLength} characters.");
		}

		// Keys belong to a user: two users can't see each other's responses.
		var cacheKey = $"idempotency:{services.GetRequiredService<ICurrentUser>().UserId}:{key}";
		var inProgress = services.GetRequiredService<KeysInProgress>();
		if (!inProgress.TryStart(cacheKey))
		{
			return Problem(StatusCodes.Status409Conflict, "Request in progress",
				"A request with this idempotency key is still being processed. Retry later.");
		}

		try
		{
			var cache = services.GetRequiredService<IDistributedCache>();
			var fingerprint = Fingerprint(invocation, requestArguments);

			if (await cache.GetAsync(cacheKey, httpContext.RequestAborted) is { } bytes)
			{
				var stored = JsonSerializer.Deserialize<StoredResponse>(bytes)!;
				return stored.Fingerprint == fingerprint
					? stored with { Replayed = true }
					: Problem(StatusCodes.Status422UnprocessableEntity, "Idempotency key reused",
						"This idempotency key was already used for a different request.");
			}

			var response = await CaptureAsync(httpContext, await next(invocation), fingerprint);

			// Only successes are kept: after a failure, a retry with the same key runs again.
			if (response.StatusCode is >= 200 and < 300)
			{
				await cache.SetAsync(cacheKey, JsonSerializer.SerializeToUtf8Bytes(response),
					new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = options.Retention },
					httpContext.RequestAborted);
			}

			return response;
		}
		finally
		{
			inProgress.Finish(cacheKey);
		}
	}

	private static string Fingerprint(EndpointFilterInvocationContext invocation, int[] requestArguments)
	{
		var request = invocation.HttpContext.Request;
		var jsonOptions = invocation.HttpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
		var values = requestArguments.Select(index => invocation.Arguments[index]).ToArray();

		var text = $"{request.Method} {request.Path}{request.QueryString}\n{JsonSerializer.Serialize(values, jsonOptions)}";
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
	}

	// Runs the handler's result into a buffer, so the response can be stored before the client gets it.
	private static async Task<StoredResponse> CaptureAsync(HttpContext httpContext, object? result, string fingerprint)
	{
		var response = httpContext.Response;
		var body = response.Body;
		using var buffer = new MemoryStream();
		response.Body = buffer;
		try
		{
			await (result as IResult ?? Results.Ok(result)).ExecuteAsync(httpContext);
		}
		finally
		{
			response.Body = body;
		}

		return new StoredResponse(fingerprint, response.StatusCode, response.ContentType, response.Headers.Location, buffer.ToArray());
	}

	private static IResult Problem(int statusCode, string title, string detail) =>
		Results.Problem(statusCode: statusCode, title: title, detail: detail, type: statusCode switch
		{
			StatusCodes.Status400BadRequest => ProblemDetailsDefaults.RFC400Type,
			StatusCodes.Status409Conflict => ProblemDetailsDefaults.RFC409Type,
			_ => null
		});

	// Concurrent requests with the same key, within this instance (see docs/idempotency.md).
	internal sealed class KeysInProgress
	{
		private readonly ConcurrentDictionary<string, byte> _keys = new();

		public bool TryStart(string key) => _keys.TryAdd(key, 0);

		public void Finish(string key) => _keys.TryRemove(key, out _);
	}
}
#endif
