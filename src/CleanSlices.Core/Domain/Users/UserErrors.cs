using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Users;

internal class UserErrors
{
	internal static readonly ProblemError EmptyName = new("User.Name", "Name cannot be empty.");
	internal static readonly ProblemError NameTooLong = new("User.Name", $"Name cannot exceed {User.NameMaxLength} characters.");
	internal static readonly ProblemError EmptyEmail = new("User.Email", "Email cannot be empty.");
	internal static readonly ProblemError EmailTooLong = new("User.Email", $"Email cannot exceed {User.EmailMaxLength} characters.");}
