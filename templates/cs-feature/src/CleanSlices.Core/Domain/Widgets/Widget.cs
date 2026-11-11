using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Widgets;

#if (soft_delete && audit)
public class Widget : Entity, ISoftDeletable, IAuditable
#elif (soft_delete)
public class Widget : Entity, ISoftDeletable
#elif (audit)
public class Widget : Entity, IAuditable
#else
public class Widget : Entity
#endif
{
	public const int NameMaxLength = 200;

	public string Name { get; private set; } = null!;
#if (soft_delete)
	public DateTimeOffset? DeletedAt { get; private set; }
#endif

	private Widget() { } // For EF Core

	private Widget(string name)
	{
		Name = name;
	}

#if (result)
	public static Result<Widget> Create(string name)
	{
		var errors = new[] { ValidateName(name) }
			.OfType<FieldError>()
			.ToList();

		if (errors.TryGetValidationError(out var validationError))
		{
			return Result.Failure<Widget>(validationError);
		}

		var entity = new Widget(name);
		entity.Raise(new WidgetCreatedDomainEvent(entity.Id));
		return Result.Success(entity);
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
		return Result.Success();
	}
#else
	public static Widget Create(string name)
	{
		DomainValidationException.ThrowIfAny(ValidateName(name));

		var entity = new Widget(name);
		entity.Raise(new WidgetCreatedDomainEvent(entity.Id));
		return entity;
	}

	public void UpdateName(string name)
	{
		DomainValidationException.ThrowIfAny(ValidateName(name));
		Name = name;
	}
#endif

	public void Delete()
	{
		Raise(new WidgetDeletedDomainEvent(Id));
	}

	private static FieldError? ValidateName(string name) =>
		string.IsNullOrWhiteSpace(name) ? WidgetErrors.EmptyName
		: name.Length > NameMaxLength ? WidgetErrors.NameTooLong
		: null;
}
