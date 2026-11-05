using CleanSlices.Core.Domain.Widgets;
using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Tests.Domain.Widgets;

public class WidgetTests
{
	[Fact]
	public void Create_with_a_valid_name_raises_created()
	{
		var entity = Widget.Create("First");

		Assert.Equal("First", entity.Name);
		var created = Assert.IsType<WidgetCreatedDomainEvent>(Assert.Single(entity.DomainEvents));
		Assert.Equal(entity.Id, created.Id);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Create_rejects_an_empty_name(string name)
	{
		var exception = Assert.Throws<DomainValidationException>(() => Widget.Create(name));

		Assert.Equal(nameof(Widget.Name), Assert.Single(exception.Errors).Field);
	}

	[Fact]
	public void Create_rejects_a_name_that_is_too_long()
	{
		var name = new string('a', Widget.NameMaxLength + 1);

		var exception = Assert.Throws<DomainValidationException>(() => Widget.Create(name));

		Assert.Equal(nameof(Widget.Name), Assert.Single(exception.Errors).Field);
	}

	[Fact]
	public void UpdateName_changes_the_name()
	{
		var entity = Widget.Create("First");

		entity.UpdateName("Second");

		Assert.Equal("Second", entity.Name);
	}

	[Fact]
	public void UpdateName_with_an_invalid_name_keeps_the_old_one()
	{
		var entity = Widget.Create("First");

		Assert.Throws<DomainValidationException>(() => entity.UpdateName(""));

		Assert.Equal("First", entity.Name);
	}

	[Fact]
	public void Delete_raises_deleted()
	{
		var entity = Widget.Create("First");
		entity.ClearDomainEvents();

		entity.Delete();

		var deleted = Assert.IsType<WidgetDeletedDomainEvent>(Assert.Single(entity.DomainEvents));
		Assert.Equal(entity.Id, deleted.Id);
	}
}
