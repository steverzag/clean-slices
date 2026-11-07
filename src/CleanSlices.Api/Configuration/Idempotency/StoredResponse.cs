#if (idempotency)
using System.Text.Json.Serialization;

namespace CleanSlices.Api.Configuration.Idempotency;

// What a successful request answered, written again for every retry with the same key.
internal sealed record StoredResponse(string Fingerprint, int StatusCode, string? ContentType, string? Location, byte[] Body) : IResult
{
	[JsonIgnore]
	public bool Replayed { get; init; }

	public async Task ExecuteAsync(HttpContext httpContext)
	{
		var response = httpContext.Response;
		response.StatusCode = StatusCode;
		response.ContentType = ContentType;
		if (Location is not null)
		{
			response.Headers.Location = Location;
		}

		if (Replayed)
		{
			response.Headers[Idempotency.ReplayedHeader] = "true";
		}

		await response.Body.WriteAsync(Body, httpContext.RequestAborted);
	}
}
#endif
