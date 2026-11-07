using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
#if (idempotency)
using CleanSlices.Api.Configuration.Idempotency;
#endif
using Microsoft.AspNetCore.Http;

namespace CleanSlices.Api.Tests.Infrastructure;

public static class HttpExtensions
{
	// Same settings as the API: camelCase names, enums as strings.
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};

	extension(HttpContent content)
	{
		public async Task<T> ReadAsAsync<T>()
		{
			var value = await content.ReadFromJsonAsync<T>(JsonOptions, TestContext.Current.CancellationToken);
			return value ?? throw new InvalidOperationException($"The response body was empty; expected {typeof(T).Name}.");
		}

		public Task<HttpValidationProblemDetails> ReadValidationProblemAsync() =>
			content.ReadAsAsync<HttpValidationProblemDetails>();
	}

	extension(HttpClient client)
	{
		public Task<HttpResponseMessage> Post<T>(string uri, T body) =>
			client.PostAsJsonAsync(uri, body, JsonOptions, TestContext.Current.CancellationToken);
#if (idempotency)

		public Task<HttpResponseMessage> Post<T>(string uri, T body, string idempotencyKey) =>
			client.SendAsync(new HttpRequestMessage(HttpMethod.Post, uri)
			{
				Content = JsonContent.Create(body, options: JsonOptions),
				Headers = { { Idempotency.Header, idempotencyKey } }
			}, TestContext.Current.CancellationToken);
#endif

		public Task<HttpResponseMessage> Patch<T>(string uri, T body) =>
			client.PatchAsJsonAsync(uri, body, JsonOptions, TestContext.Current.CancellationToken);

		public Task<HttpResponseMessage> Get(string uri) =>
			client.GetAsync(uri, TestContext.Current.CancellationToken);

		public Task<HttpResponseMessage> Delete(string uri) =>
			client.DeleteAsync(uri, TestContext.Current.CancellationToken);

		public Task<HttpResponseMessage> Patch(string uri) =>
			client.PatchAsync(uri, content: null, TestContext.Current.CancellationToken);

		public Task<HttpResponseMessage> Post(string uri) =>
			client.PostAsync(uri, content: null, TestContext.Current.CancellationToken);
	}
}
