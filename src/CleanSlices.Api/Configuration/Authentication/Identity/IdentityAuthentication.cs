#if (identity)
using CleanSlices.Infrastructure.Identity;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OpenApi;

namespace CleanSlices.Api.Configuration.Authentication.Identity;

public static class IdentityAuthentication
{
	public const string RoutePrefix = "/account";

	extension(IHostApplicationBuilder builder)
	{
		// Bearer tokens only: an API has no use for Identity's cookies.
		public IHostApplicationBuilder AddIdentityAuthentication()
		{
			builder.Services.AddAuthentication(IdentityConstants.BearerScheme)
				.AddBearerToken(IdentityConstants.BearerScheme);

			builder.Services.AddIdentityCore<AppUser>()
				.AddRoles<IdentityRole>()
				.AddEntityFrameworkStores<AppDbContext>()
				.AddApiEndpoints();

			builder.Services.AddLoginRequired();
			builder.Services.Configure<OpenApiOptions>("v1", options =>
				options.AddDocumentTransformer<IdentityOpenApi>());

			return builder;
		}
	}

	extension(IEndpointRouteBuilder app)
	{
		// register, login, refresh, confirmEmail, forgotPassword, resetPassword and manage/*.
		public IEndpointConventionBuilder MapIdentityEndpoints()
		{
			var account = app.MapGroup(RoutePrefix).WithTags("Account").MapIdentityApi<AppUser>();

			// Identity's endpoints don't opt out of the fallback policy: open all but manage/*, which requires a login.
			account.Add(endpoint =>
			{
				if (endpoint is RouteEndpointBuilder route && route.RoutePattern.RawText?.Contains("manage/") != true)
				{
					endpoint.Metadata.Add(new AllowAnonymousAttribute());
				}
			});

			return account;
		}
	}
}
#endif
