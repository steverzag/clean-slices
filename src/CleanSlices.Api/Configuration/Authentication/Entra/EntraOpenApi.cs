#if (entra)
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CleanSlices.Api.Configuration.Authentication.Entra;

// Describes the login in the OpenAPI document, so API reference pages can sign in.
internal sealed class EntraOpenApi(EntraOptions entra) : IOpenApiDocumentTransformer
{
	public const string SchemeName = "entra";

	public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
	{
		var endpoints = $"{entra.Instance.TrimEnd('/')}/{entra.TenantId}/oauth2/v2.0";

		document.Components ??= new OpenApiComponents();
		document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
		document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
		{
			Type = SecuritySchemeType.OAuth2,
			Description = "Microsoft Entra ID login (authorization code with PKCE).",
			Flows = new OpenApiOAuthFlows
			{
				AuthorizationCode = new OpenApiOAuthFlow
				{
					AuthorizationUrl = new Uri($"{endpoints}/authorize"),
					TokenUrl = new Uri($"{endpoints}/token"),
					Scopes = new Dictionary<string, string> { [$"api://{entra.ClientId}/{entra.Scope}"] = "Call the API as the signed-in user" }
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
