using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Users;

public class User : Entity
{
	public const int NameMaxLength = 200;
	public const int EmailMaxLength = 256;

	public string Name { get; private set; } = null!;
	public string Email { get; private set; } = null!;

	private User() { } // For EF Core

	private User(string name, string email)
	{
		Name = name;
		Email = email;
	}

	public static User Create(string name, string email)
	{
		DomainValidationException.ThrowIfAny(ValidateName(name), ValidateEmail(email));

		var user = new User(name, email);
		user.Raise(new UserCreatedDomainEvent(user.Id));
		return user;
	}

	public void UpdateName(string name)
	{
		DomainValidationException.ThrowIfAny(ValidateName(name));
		Name = name;
	}

	public void UpdateEmail(string email)
	{
		DomainValidationException.ThrowIfAny(ValidateEmail(email));
		Email = email;
	}

	public void Delete()
	{
		Raise(new UserDeletedDomainEvent(Id));
	}

	private static DomainError? ValidateName(string name) =>
		string.IsNullOrWhiteSpace(name) ? UserErrors.EmptyName
		: name.Length > NameMaxLength ? UserErrors.NameTooLong
		: null;

	private static DomainError? ValidateEmail(string email) =>
		string.IsNullOrWhiteSpace(email) ? UserErrors.EmptyEmail
		: email.Length > EmailMaxLength ? UserErrors.EmailTooLong
		: !EmailValidator.IsValid(email) ? UserErrors.InvalidEmail
		: null;
}
