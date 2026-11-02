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

	public static Result<User> Create(string name, string email)
	{
		var errors = new[] { ValidateName(name), ValidateEmail(email) }
			.OfType<Error>()
			.ToList();

		if (errors.TryGetValidationError(out var validationError))
		{
			return Result.Failure<User>(validationError!);
		}

		var user = new User(name, email);
		user.Raise(new UserCreatedDomainEvent(user.Id));
		return user;
	}

	public Result UpdateName(string name)
	{
		if (ValidateName(name) is { } error)
		{
			return Result.Failure(error);
		}

		Name = name;
		return Result.Success();
	}

	public Result UpdateEmail(string email)
	{
		if (ValidateEmail(email) is { } error)
		{
			return Result.Failure(error);
		}

		Email = email;
		return Result.Success();
	}

	public void Delete()
	{
		Raise(new UserDeletedDomainEvent(Id));
	}

	private static Error? ValidateName(string name) =>
		string.IsNullOrWhiteSpace(name) ? UserErrors.EmptyName
		: name.Length > NameMaxLength ? UserErrors.NameTooLong
		: null;

	private static Error? ValidateEmail(string email) =>
		string.IsNullOrWhiteSpace(email) ? UserErrors.EmptyEmail
		: email.Length > EmailMaxLength ? UserErrors.EmailTooLong
		: null;
}
