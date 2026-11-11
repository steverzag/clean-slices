using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Users;

#if (soft_delete && audit)
public class User : Entity, ISoftDeletable, IAuditable
#elif (soft_delete)
public class User : Entity, ISoftDeletable
#elif (audit)
public class User : Entity, IAuditable
#else
public class User : Entity
#endif
{
	public const int NameMaxLength = 200;
	public const int EmailMaxLength = 256;

	public string Name { get; private set; } = null!;
#if (audit)
	// Personal data: kept out of the audit snapshots (see docs/audit.md).
	[NotAudited]
#endif
	public string Email { get; private set; } = null!;
#if (soft_delete)
	public DateTimeOffset? DeletedAt { get; private set; }
#endif

	private User() { } // For EF Core

	private User(string name, string email)
	{
		Name = name;
		Email = email;
	}

#if (result)
	public static Result<User> Create(string name, string email)
	{
		var errors = new[] { ValidateName(name), ValidateEmail(email) }
			.OfType<FieldError>()
			.ToList();

		if (errors.TryGetValidationError(out var validationError))
		{
			return Result.Failure<User>(validationError);
		}

		var user = new User(name, email);
		user.Raise(new UserCreatedDomainEvent(user.Id));
		return user;
	}

	public Result UpdateName(string name)
	{
		var errors = new[] { ValidateName(name) }
			.OfType<FieldError>()
			.ToList();

		if (errors.TryGetValidationError(out var validationError))
		{
			return Result.Failure(validationError);
		}

		Name = name;
		Raise(new UserUpdatedDomainEvent(Id));
		return Result.Success();
	}

	public Result UpdateEmail(string email)
	{
		var errors = new[] { ValidateEmail(email) }
			.OfType<FieldError>()
			.ToList();

		if (errors.TryGetValidationError(out var validationError))
		{
			return Result.Failure(validationError);
		}

		Email = email;
		Raise(new UserUpdatedDomainEvent(Id));
		return Result.Success();
	}
#else
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
		Raise(new UserUpdatedDomainEvent(Id));
	}

	public void UpdateEmail(string email)
	{
		DomainValidationException.ThrowIfAny(ValidateEmail(email));
		Email = email;
		Raise(new UserUpdatedDomainEvent(Id));
	}
#endif

	public void Delete()
	{
		Raise(new UserDeletedDomainEvent(Id));
	}

	private static FieldError? ValidateName(string name) =>
		string.IsNullOrWhiteSpace(name) ? UserErrors.EmptyName
		: name.Length > NameMaxLength ? UserErrors.NameTooLong
		: null;

	private static FieldError? ValidateEmail(string email) =>
		string.IsNullOrWhiteSpace(email) ? UserErrors.EmptyEmail
		: email.Length > EmailMaxLength ? UserErrors.EmailTooLong
		: !EmailValidator.IsValid(email) ? UserErrors.InvalidEmail
		: null;
}
