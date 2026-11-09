#if (serilog)
using System.Collections.Concurrent;
using System.Net;
#if (authentication)
using CleanSlices.Api.Tests.Authentication;
#endif
using CleanSlices.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace CleanSlices.Api.Tests.Configuration;

// Serilog picks up sinks registered in the services, so the test can record what the API logs.
public class RequestLoggingTests(ApiFactory factory)
{
	[Fact]
	public async Task Each_request_is_logged_once_with_its_path_status_and_user()
	{
		var sink = new RecordingSink();
		await using var host = factory.WithWebHostBuilder(builder =>
			builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(sink)));

		var response = await host.CreateClient().GetAsync("/alive", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var request = Assert.Single(sink.RequestsTo("/alive"));
		Assert.Equal("200", request.Properties["StatusCode"].ToString());
		Assert.Equal("\"GET\"", request.Properties["RequestMethod"].ToString());
		Assert.True(request.Properties.ContainsKey("Elapsed"));
		Assert.True(request.Properties.ContainsKey("UserId"));
		Assert.NotNull(request.TraceId);
	}
#if (authentication)
	//#if (samples)

	[Fact]
	public async Task A_logged_in_request_is_logged_with_the_user_id()
	{
		var sink = new RecordingSink();
		await using var host = factory.WithWebHostBuilder(builder =>
			builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(sink)));

		await host.CreateClient().GetAsync("/users", TestContext.Current.CancellationToken);

		var request = Assert.Single(sink.RequestsTo("/users"));
		Assert.Equal($"\"{await factory.UserIdAsync(TestUsers.User)}\"", request.Properties["UserId"].ToString());
	}
	//#endif
#endif

	private sealed class RecordingSink : ILogEventSink
	{
		private readonly ConcurrentQueue<LogEvent> _events = new();

		public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

		// The request line Serilog writes when a request completes.
		public IEnumerable<LogEvent> RequestsTo(string path) => _events.Where(e =>
			e.MessageTemplate.Text.StartsWith("HTTP ", StringComparison.Ordinal)
			&& e.Properties.TryGetValue("RequestPath", out var value)
			&& value.ToString() == $"\"{path}\"");
	}
}
#endif
