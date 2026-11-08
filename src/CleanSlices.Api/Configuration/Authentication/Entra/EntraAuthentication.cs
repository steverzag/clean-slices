#if (entra)
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using System.Security.Claims;

namespace CleanSlices.Api.Configuration.Authentication.Entra;

public static class EntraAuthentication
{
	private const string ObjectIdClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

	extension(IHostApplicationBuilder builder)
	{
		public IHostApplicationBuilder AddEntraAuthentication()
		{
			var entra = builder.Configuration.GetSection(EntraOptions.Section).Get<EntraOptions>() ?? new();
			if (string.IsNullOrWhiteSpace(entra.TenantId) || string.IsNullOrWhiteSpace(entra.ClientId))
			{
				throw new InvalidOperationException(
					$"Set {EntraOptions.Section}:TenantId and {EntraOptions.Section}:ClientId to your app registration's IDs (see docs/authentication.md).");
			}

			builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
				.AddJwtBearer(options =>
				{
					options.Authority = entra.Authority;
					options.TokenValidationParameters.ValidIssuers = entra.Issuers;
					options.TokenValidationParameters.ValidAudiences = entra.Audiences;
					options.Events = new JwtBearerEvents { OnTokenValidated = UseObjectIdAsUserId };
				});

			builder.Services.AddLoginRequired();
			builder.Services.Configure<OpenApiOptions>("v1", options =>
				options.AddDocumentTransformer(new EntraOpenApi(entra)));

			return builder;
		}
	}

	// Entra's subject differs per app; the object ID is the user's ID in the tenant.
	private static Task UseObjectIdAsUserId(TokenValidatedContext context)
	{
		if (context.Principal?.Identity is ClaimsIdentity identity
			&& identity.FindFirst(ObjectIdClaim) is { } objectId)
		{
			foreach (var subject in identity.FindAll(ClaimTypes.NameIdentifier).ToList())
			{
				identity.RemoveClaim(subject);
			}

			identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, objectId.Value));
		}

		return Task.CompletedTask;
	}
}
#endif
