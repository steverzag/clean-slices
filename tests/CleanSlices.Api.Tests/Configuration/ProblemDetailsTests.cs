using System.Net;
using CleanSlices.Api.Configuration;
#if (result)
using CleanSlices.Api.Extensions;
#endif
using CleanSlices.Api.Tests.Infrastructure;
#if (result)
using CleanSlices.Core.Shared;
#endif
using Microsoft.AspNetCore.Http;
#if (result)
using Microsoft.AspNetCore.Http.HttpResults;
#endif
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

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
	public void Nested_validation_fields_use_json_names_in_every_segment()
	{
		using var scope = factory.Services.CreateScope();
		var problem = new HttpValidationProblemDetails(new Dictionary<string, string[]> { ["Lines[0].ProductName"] = ["Required."] });

		ProblemDetailsCustomization.Apply(new ProblemDetailsContext
		{
			HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider },
			ProblemDetails = problem
		});

		Assert.Equal(["lines[0].productName"], problem.Errors.Keys);
	}
#if (result)

	[Fact]
	public void An_error_of_an_unknown_type_is_a_500_without_its_text()
	{
		var problem = Assert.IsType<ProblemHttpResult>(new Error("Payments.KeyRejected", "Key sk_live_123 rejected").ToProblem());

		Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
		Assert.NotEqual("Payments.KeyRejected", problem.ProblemDetails.Title);
		Assert.DoesNotContain("sk_live_123", problem.ProblemDetails.Detail);
	}
#endif

	[Fact]
	public async Task Health_endpoint_reports_healthy_with_the_database_up()
	{
		var response = await _client.Get("/health");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}
}
