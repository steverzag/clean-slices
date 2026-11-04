using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Users;

internal class UserErrors
{
	internal static readonly FieldError EmptyName = new(nameof(User.Name), "Name cannot be empty.");
	internal static readonly FieldError NameTooLong = new(nameof(User.Name), $"Name cannot exceed {User.NameMaxLength} characters.");
	internal static readonly FieldError EmptyEmail = new(nameof(User.Email), "Email cannot be empty.");
	internal static readonly FieldError EmailTooLong = new(nameof(User.Email), $"Email cannot exceed {User.EmailMaxLength} characters.");
	internal static readonly FieldError InvalidEmail = new(nameof(User.Email), "Email is not a valid email address.");
}
