#if (identity)
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CleanSlices.Api.Configuration.Authentication.Identity;

// Describes the login in the OpenAPI document, so API reference pages can send the token.
internal sealed class IdentityOpenApi : IOpenApiDocumentTransformer
{
	public const string SchemeName = "identity";

	public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
	{
		document.Components ??= new OpenApiComponents();
		document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
		document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
		{
			Type = SecuritySchemeType.Http,
			Scheme = "bearer",
			Description = $"The accessToken from POST {IdentityAuthentication.RoutePrefix}/login.",
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
