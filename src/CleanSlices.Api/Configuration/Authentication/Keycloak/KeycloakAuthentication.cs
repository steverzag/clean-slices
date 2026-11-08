#if (keycloak)
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using System.Security.Claims;
using System.Text.Json;

namespace CleanSlices.Api.Configuration.Authentication.Keycloak;

public static class KeycloakAuthentication
{
	extension(IHostApplicationBuilder builder)
	{
		public IHostApplicationBuilder AddKeycloakAuthentication()
		{
			var keycloak = builder.Configuration.GetSection(KeycloakOptions.Section).Get<KeycloakOptions>() ?? new();

			builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
				.AddJwtBearer(options =>
				{
					options.Authority = keycloak.Authority;
					options.Audience = keycloak.Audience;
					options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
					options.Events = new JwtBearerEvents { OnTokenValidated = AddRealmRoles };
				});

			builder.Services.AddLoginRequired();
			builder.Services.Configure<OpenApiOptions>("v1", options =>
				options.AddDocumentTransformer(new KeycloakOpenApi(keycloak)));

			return builder;
		}
	}

	// Keycloak sends realm roles as {"realm_access": {"roles": [...]}}; ASP.NET Core only reads flat role claims.
	private static Task AddRealmRoles(TokenValidatedContext context)
	{
		if (context.Principal?.Identity is ClaimsIdentity identity
			&& identity.FindFirst("realm_access") is { } realmAccess)
		{
			using var json = JsonDocument.Parse(realmAccess.Value);
			if (json.RootElement.TryGetProperty("roles", out var roles))
			{
				foreach (var role in roles.EnumerateArray())
				{
					identity.AddClaim(new Claim(identity.RoleClaimType, role.GetString()!));
				}
			}
		}

		return Task.CompletedTask;
	}
}
#endif
