using System.Net;
using CleanSlices.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CleanSlices.Api.Tests.Configuration;

public class ProblemDetailsTests(ApiFactory factory)
{
	private readonly HttpClient _client = factory.CreateClient();

	[Fact]
	public async Task Errors_are_problem_details_with_request_information()
	{
		var response = await _client.Get("/does-not-exist");

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
		Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

		var problem = await response.Content.ReadAsAsync<ProblemDetails>();
		Assert.Equal("GET /does-not-exist", problem.Instance);
		Assert.True(problem.Extensions.ContainsKey("requestId"));
		Assert.True(problem.Extensions.ContainsKey("traceId"));
	}

	[Fact]
	public async Task Health_endpoint_reports_healthy_with_the_database_up()
	{
		var response = await _client.Get("/health");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}
}
