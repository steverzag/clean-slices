namespace CleanSlices.Api.Configuration;

internal sealed class ProblemDetailsDefaults
{
	internal const string RFC400Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
	internal const string RFC403Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3";
	internal const string RFC404Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4";
	internal const string RFC409Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8";
	internal const string RFC500Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1";
	internal const string RFC503Type = "https://tools.ietf.org/html/rfc7231#section-6.6.4";
	internal const string RFC500Title = "Server Error";
	internal const string RFC500Details = "An unexpected error occurred.";
}
