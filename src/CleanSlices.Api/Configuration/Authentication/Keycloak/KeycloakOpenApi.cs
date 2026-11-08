#if (keycloak)
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CleanSlices.Api.Configuration.Authentication.Keycloak;

// Describes the login in the OpenAPI document, so API reference pages can sign in.
internal sealed class KeycloakOpenApi(KeycloakOptions keycloak) : IOpenApiDocumentTransformer
{
	public const string SchemeName = "keycloak";

	public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
	{
		document.Components ??= new OpenApiComponents();
		document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
		document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
		{
			Type = SecuritySchemeType.OAuth2,
			Description = "Keycloak login (authorization code with PKCE).",
			Flows = new OpenApiOAuthFlows
			{
				AuthorizationCode = new OpenApiOAuthFlow
				{
					AuthorizationUrl = new Uri($"{keycloak.Authority}/protocol/openid-connect/auth"),
					TokenUrl = new Uri($"{keycloak.Authority}/protocol/openid-connect/token"),
					Scopes = new Dictionary<string, string> { ["openid"] = "OpenID Connect" }
				}
			}
		};

		document.Security ??= [];
		document.Security.Add(new OpenApiSecurityRequirement
		{
			[new OpenApiSecuritySchemeReference(SchemeName, document)] = []
		});

		return Task.CompletedTask;
	}
}
#endif
