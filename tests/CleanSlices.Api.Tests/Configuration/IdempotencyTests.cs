#if (idempotency)
using System.Net;
using System.Net.Http.Json;
using CleanSlices.Api.Configuration.Idempotency;
using CleanSlices.Infrastructure.CurrentUser;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CleanSlices.Api.Tests.Configuration;

// The filter on a small app of its own, so every rule can be checked without a database.
public sealed class IdempotencyTests : IAsyncDisposable
{
	private const string UserHeader = "X-Test-User";

	public sealed record Probe(string Name);

	private readonly TaskCompletionSource _slowRequestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly TaskCompletionSource _releaseSlowRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private WebApplication? _app;
	private int _runs;

	[Fact]
	public async Task A_retry_with_the_same_key_gets_the_stored_response()
	{
		var client = await StartAsync();
		var key = NewKey();

		var first = await PostAsync(client, new Probe("Ana"), key);
		var retry = await PostAsync(client, new Probe("Ana"), key);

		Assert.Equal(1, _runs);
		Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
		Assert.Equal(first.Headers.Location, retry.Headers.Location);
		Assert.Equal(first.Content.Headers.ContentType, retry.Content.Headers.ContentType);
		Assert.Equal(await Body(first), await Body(retry));
		Assert.False(first.Headers.Contains(Idempotency.ReplayedHeader));
		Assert.Equal(["true"], retry.Headers.GetValues(Idempotency.ReplayedHeader));
	}

	[Fact]
	public async Task The_same_key_for_a_different_request_returns_422()
	{
		var client = await StartAsync();
		var key = NewKey();
		await PostAsync(client, new Probe("Ana"), key);

		var response = await PostAsync(client, new Probe("Luis"), key);

		Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
		Assert.Equal(1, _runs);
	}

	[Fact]
	public async Task A_key_whose_first_request_is_still_running_returns_409()
	{
		var client = await StartAsync();
		var key = NewKey();
		var slow = PostAsync(client, new Probe("slow"), key);
		await _slowRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

		var response = await PostAsync(client, new Probe("slow"), key);
		_releaseSlowRequest.SetResult();

		Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
		Assert.Equal(HttpStatusCode.Created, (await slow).StatusCode);
	}

	[Fact]
	public async Task A_failure_isnt_stored_so_a_retry_runs_again()
	{
		var client = await StartAsync();
		var key = NewKey();

		await PostAsync(client, new Probe("fail"), key);
		var retry = await PostAsync(client, new Probe("fail"), key);

		Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
		Assert.Equal(2, _runs);
	}

	[Fact]
	public async Task Requests_without_a_key_always_run()
	{
		var client = await StartAsync();

		await PostAsync(client, new Probe("Ana"), key: null);
		await PostAsync(client, new Probe("Ana"), key: null);

		Assert.Equal(2, _runs);
	}

	[Fact]
	public async Task A_missing_key_returns_400_when_keys_are_required()
	{
		var client = await StartAsync(required: true);

		var response = await PostAsync(client, new Probe("Ana"), key: null);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Equal(0, _runs);
	}

	[Fact]
	public async Task A_key_that_is_too_long_returns_400()
	{
		var client = await StartAsync();

		var response = await PostAsync(client, new Probe("Ana"), new string('k', Idempotency.MaxKeyLength + 1));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Equal(0, _runs);
	}

	[Fact]
	public async Task Keys_belong_to_a_user()
	{
		var client = await StartAsync();
		var key = NewKey();

		await PostAsync(client, new Probe("Ana"), key, user: "ana");
		var other = await PostAsync(client, new Probe("Ana"), key, user: "luis");

		Assert.False(other.Headers.Contains(Idempotency.ReplayedHeader));
		Assert.Equal(2, _runs);
	}

	[Fact]
	public async Task A_colon_in_a_key_or_a_user_doesnt_mix_two_users_up()
	{
		var client = await StartAsync();

		await PostAsync(client, new Probe("Ana"), "b:c", user: "a");
		var other = await PostAsync(client, new Probe("Ana"), "c", user: "a:b");

		Assert.False(other.Headers.Contains(Idempotency.ReplayedHeader));
		Assert.Equal(2, _runs);
	}

	[Fact]
	public async Task The_response_is_stored_even_when_the_client_has_gone()
	{
		var client = await StartAsync();
		var key = NewKey();

		await Assert.ThrowsAnyAsync<Exception>(() => PostAsync(client, new Probe("gone"), key));
		HttpResponseMessage retry;
		do
		{
			retry = await PostAsync(client, new Probe("gone"), key);
		}
		while (retry.StatusCode == HttpStatusCode.Conflict);

		Assert.Equal(1, _runs);
		Assert.Equal(["true"], retry.Headers.GetValues(Idempotency.ReplayedHeader));
	}

	private async Task<HttpClient> StartAsync(bool required = false)
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseTestServer();
		builder.Configuration["Idempotency:Required"] = required.ToString();
		builder.Services.AddProblemDetails();
		builder.Services.AddSingleton<IDistributedCache>(new TokenAwareCache());
		builder.Services.AddHttpContextAccessor();
		builder.Services.AddScoped<ICurrentUser>(services =>
			new TestUser(services.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Headers[UserHeader].ToString()));
		builder.Services.AddIdempotency();

		_app = builder.Build();
		_app.MapPost("/probes/{id:int}", Handler).WithIdempotency();
		await _app.StartAsync(TestContext.Current.CancellationToken);

		return _app.GetTestClient();
	}

	private async Task<IResult> Handler(int id, Probe probe, HttpContext httpContext)
	{
		var run = Interlocked.Increment(ref _runs);
		if (probe.Name == "gone")
		{
			httpContext.Abort();
		}

		if (probe.Name == "slow")
		{
			_slowRequestStarted.SetResult();
			await _releaseSlowRequest.Task;
		}

		return probe.Name == "fail"
			? Results.Conflict()
			: Results.Created($"/probes/{id}", new { id, probe.Name, run });
	}

	private static Task<HttpResponseMessage> PostAsync(HttpClient client, Probe probe, string? key, string user = "ana")
	{
		var request = new HttpRequestMessage(HttpMethod.Post, "/probes/7") { Content = JsonContent.Create(probe) };
		request.Headers.Add(UserHeader, user);
		if (key is not null)
		{
			request.Headers.Add(Idempotency.Header, key);
		}

		return client.SendAsync(request, TestContext.Current.CancellationToken);
	}

	private static Task<string> Body(HttpResponseMessage response) =>
		response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

	private static string NewKey() => Guid.NewGuid().ToString();

	private sealed record TestUser(string? UserId) : ICurrentUser
	{
		public bool IsAuthenticated => UserId is not null;
	}

	// Like Redis, and unlike the in-memory cache: it stops when the token is cancelled.
	private sealed class TokenAwareCache : IDistributedCache
	{
		private readonly MemoryDistributedCache _inner = new(Options.Create(new MemoryDistributedCacheOptions()));

		public byte[]? Get(string key) => _inner.Get(key);

		public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => _inner.GetAsync(key, token);

		public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _inner.Set(key, value, options);

		public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
		{
			token.ThrowIfCancellationRequested();
			return _inner.SetAsync(key, value, options, token);
		}

		public void Refresh(string key) => _inner.Refresh(key);

		public Task RefreshAsync(string key, CancellationToken token = default) => _inner.RefreshAsync(key, token);

		public void Remove(string key) => _inner.Remove(key);

		public Task RemoveAsync(string key, CancellationToken token = default) => _inner.RemoveAsync(key, token);
	}

	public async ValueTask DisposeAsync()
	{
		if (_app is not null)
		{
			await _app.DisposeAsync();
		}
	}
}
#endif
