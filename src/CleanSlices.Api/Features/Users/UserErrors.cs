using CleanSlices.Core.Shared;

namespace CleanSlices.Api.Features.Users;

internal static class UserErrors
{
#if (result)
	internal static NotFoundError NotFound(Guid id) => new("User.NotFound", $"User '{id}' was not found.");

	internal static ConflictError EmailInUse(string email) => new("User.EmailInUse", $"Email '{email}' is already in use.");
#else
	internal static NotFoundException NotFound(Guid id) => new("User.NotFound", $"User '{id}' was not found.");

	internal static ConflictException EmailInUse(string email) => new("User.EmailInUse", $"Email '{email}' is already in use.");
#endif
}
