using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Widgets;

public class Widget : Entity
{
	public const int NameMaxLength = 200;

	public string Name { get; private set; } = null!;

	private Widget() { } // For EF Core

	private Widget(string name)
	{
		Name = name;
	}

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

	public void Delete()
	{
		Raise(new WidgetDeletedDomainEvent(Id));
	}

	private static DomainError? ValidateName(string name) =>
		string.IsNullOrWhiteSpace(name) ? WidgetErrors.EmptyName
		: name.Length > NameMaxLength ? WidgetErrors.NameTooLong
		: null;
}
