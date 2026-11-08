#if (authentication)
using CleanSlices.Infrastructure.CurrentUser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleanSlices.Api.Configuration.Authentication;

public static class AuthenticationRegistration
{
	extension(IServiceCollection services)
	{
		// Every endpoint requires a logged-in user unless it allows anonymous access.
		public IServiceCollection AddLoginRequired()
		{
			services.AddHttpContextAccessor();
			services.TryAddSingleton<ICurrentUser, ClaimsCurrentUser>();
			services.AddAuthorizationBuilder()
				.SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

			return services;
		}
	}
}
#endif
