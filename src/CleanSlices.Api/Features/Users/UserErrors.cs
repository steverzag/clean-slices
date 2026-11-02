using CleanSlices.Core.Shared;

namespace CleanSlices.Api.Features.Users;

internal static class UserErrors
{
	internal static NotFoundException NotFound(Guid id) => new("User.NotFound", $"User '{id}' was not found.");

	internal static ConflictException EmailInUse(string email) => new("User.EmailInUse", $"Email '{email}' is already in use.");
}
