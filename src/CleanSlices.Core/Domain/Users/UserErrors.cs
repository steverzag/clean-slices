using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Users;

internal class UserErrors
{
	internal static readonly DomainError EmptyName = new("User.Name", "Name cannot be empty.");
	internal static readonly DomainError NameTooLong = new("User.Name", $"Name cannot exceed {User.NameMaxLength} characters.");
	internal static readonly DomainError EmptyEmail = new("User.Email", "Email cannot be empty.");
	internal static readonly DomainError EmailTooLong = new("User.Email", $"Email cannot exceed {User.EmailMaxLength} characters.");
	internal static readonly DomainError InvalidEmail = new("User.Email", "Email is not a valid email address.");
}
