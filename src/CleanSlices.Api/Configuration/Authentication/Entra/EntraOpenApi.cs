#if (entra)
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

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
					Scopes = new Dictionary<string, string> { [$"api://{entra.ClientId}/{entra.Scope}"] = "Call the API as the signed-in user" },
					// Read by API reference pages such as Scalar, which can then sign in with this client.
					Extensions = new Dictionary<string, IOpenApiExtension>
					{
						["x-scalar-client-id"] = new JsonNodeExtension(JsonValue.Create(entra.ClientId)),
						["x-usePkce"] = new JsonNodeExtension(JsonValue.Create("SHA-256")),
					}
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
