#if (current_user)
namespace CleanSlices.Infrastructure.CurrentUser;

public interface ICurrentUser
{
	public string? UserId { get; }
	public bool IsAuthenticated { get; }
}
#endif
