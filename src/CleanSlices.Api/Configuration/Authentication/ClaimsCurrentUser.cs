#if (authentication)
using CleanSlices.Infrastructure.CurrentUser;
using System.Security.Claims;

namespace CleanSlices.Api.Configuration.Authentication;

// Outside a request (background work) there is no user: anonymous.
internal sealed class ClaimsCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
	private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

	public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);
	public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
#endif
