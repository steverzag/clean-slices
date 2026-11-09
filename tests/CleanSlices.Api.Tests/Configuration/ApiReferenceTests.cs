#if (scalar)
using System.Net;
using CleanSlices.Api.Tests.Infrastructure;

namespace CleanSlices.Api.Tests.Configuration;

public class ApiReferenceTests(ApiFactory factory)
{
	[Fact]
	public async Task The_api_reference_page_is_served_without_a_login()
	{
		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Authorization = null;

		var response = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
	}
}
#endif
