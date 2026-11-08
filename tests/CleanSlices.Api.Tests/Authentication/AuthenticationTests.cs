#if (authentication)
using System.Net;
using System.Net.Http.Headers;
#if (entra)
using CleanSlices.Api.Configuration.Authentication.Entra;
#endif
#if (keycloak)
using CleanSlices.Api.Configuration.Authentication.Keycloak;
#endif
using CleanSlices.Api.Tests.Infrastructure;
using CleanSlices.Infrastructure.CurrentUser;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CleanSlices.Api.Tests.Authentication;

public class AuthenticationTests(ApiFactory factory) : IAsyncDisposable
{
	private WebApplication? _app;

	[Fact]
	public async Task Health_checks_and_the_OpenAPI_document_need_no_login()
	{
		var client = Anonymous(factory.CreateClient());

		var alive = await client.GetAsync("/alive", TestContext.Current.CancellationToken);
		var document = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
		Assert.Equal(HttpStatusCode.OK, document.StatusCode);
		Assert.Contains("securitySchemes", await document.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
	}
	//#if (samples)

	[Fact]
	public async Task Endpoints_without_a_login_get_401_as_problem_details()
	{
		var client = Anonymous(factory.CreateClient());

		var response = await client.GetAsync("/users", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
	}
	//#endif

	[Fact]
	public async Task An_invalid_token_gets_401()
	{
		var client = await StartAsync();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");

		var response = await client.GetAsync("/me", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task The_current_user_is_the_login_ID()
	{
		var token = await factory.LoginAsync(TestUsers.User);
		var client = await StartAsync(token);

		var userId = await client.GetStringAsync("/me", TestContext.Current.CancellationToken);

#if (entra)
		// The object ID, not the subject: Entra's subject differs per app.
		Assert.Equal(new JsonWebToken(token).GetClaim("oid").Value, userId);
#else
		Assert.Equal(new JsonWebToken(token).Subject, userId);
#endif
	}

	[Fact]
	public async Task Provider_roles_become_role_claims()
	{
		var admin = await StartAsync(await factory.LoginAsync(TestUsers.Admin));
		var user = await StartAsync(await factory.LoginAsync(TestUsers.User));

		var asAdmin = await admin.GetAsync("/admin", TestContext.Current.CancellationToken);
		var asUser = await user.GetAsync("/admin", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, asAdmin.StatusCode);
		Assert.Equal(HttpStatusCode.Forbidden, asUser.StatusCode);
	}

	private static HttpClient Anonymous(HttpClient client)
	{
		client.DefaultRequestHeaders.Authorization = null;
		return client;
	}

	// A small app with the option's authentication and two probe endpoints, so roles and the user ID can be checked.
	private async Task<HttpClient> StartAsync(string? token = null)
	{
		if (_app is null)
		{
			var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
			builder.WebHost.UseTestServer();
#if (keycloak)
			builder.Configuration[$"{KeycloakOptions.Section}:Authority"] = factory.KeycloakAuthority;
			builder.AddKeycloakAuthentication();
#elif (entra)
			factory.UseEntraSettings(builder.Configuration);
			builder.AddEntraAuthentication();
			factory.TrustTestTokens(builder.Services);
#endif

			_app = builder.Build();
			_app.UseAuthentication();
			_app.UseAuthorization();
			_app.MapGet("/me", (ICurrentUser user) => user.UserId);
			_app.MapGet("/admin", () => "ok").RequireAuthorization(policy => policy.RequireRole(TestUsers.AdminRole));
			await _app.StartAsync(TestContext.Current.CancellationToken);
		}

		var client = _app.GetTestClient();
		if (token is not null)
		{
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		}

		return client;
	}

	public async ValueTask DisposeAsync()
	{
		if (_app is not null)
		{
			await _app.DisposeAsync();
		}

		GC.SuppressFinalize(this);
	}
}
#endif
