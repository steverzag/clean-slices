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

	public static Result<Widget> Create(string name)
	{
		if (ValidateName(name) is { } error)
		{
			return Result.Failure<Widget>(error);
		}

		var entity = new Widget(name);
		entity.Raise(new WidgetCreatedDomainEvent(entity.Id));
		return entity;
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

	public void Delete()
	{
		Raise(new WidgetDeletedDomainEvent(Id));
	}

	private static Error? ValidateName(string name) =>
		string.IsNullOrWhiteSpace(name) ? WidgetErrors.EmptyName
		: name.Length > NameMaxLength ? WidgetErrors.NameTooLong
		: null;
}
