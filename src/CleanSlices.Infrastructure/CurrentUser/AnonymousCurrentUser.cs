#if (current_user && !authentication)
namespace CleanSlices.Infrastructure.CurrentUser;

// Used until an authentication option provides the user from the request's claims.
internal sealed class AnonymousCurrentUser : ICurrentUser
{
	public string? UserId => null;
	public bool IsAuthenticated => false;
}
#endif
