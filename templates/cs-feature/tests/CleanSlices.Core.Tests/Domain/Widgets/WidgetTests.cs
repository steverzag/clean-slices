using CleanSlices.Core.Domain.Widgets;
#if (!result)
using CleanSlices.Core.Shared;
#endif

namespace CleanSlices.Core.Tests.Domain.Widgets;

public class WidgetTests
{
	[Fact]
	public void Create_with_a_valid_name_raises_created()
	{
#if (result)
		var entity = Widget.Create("First").ShouldSucceedWithValue();
#else
		var entity = Widget.Create("First");
#endif

		Assert.Equal("First", entity.Name);
		var created = Assert.IsType<WidgetCreatedDomainEvent>(Assert.Single(entity.DomainEvents));
		Assert.Equal(entity.Id, created.Id);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Create_rejects_an_empty_name(string name)
	{
#if (result)
		var errors = Widget.Create(name).ShouldFailValidation();

		Assert.Equal(nameof(Widget.Name), Assert.Single(errors).Field);
#else
		var exception = Assert.Throws<DomainValidationException>(() => Widget.Create(name));

		Assert.Equal(nameof(Widget.Name), Assert.Single(exception.Errors).Field);
#endif
	}

	[Fact]
	public void Create_rejects_a_name_that_is_too_long()
	{
		var name = new string('a', Widget.NameMaxLength + 1);

#if (result)
		var errors = Widget.Create(name).ShouldFailValidation();

		Assert.Equal(nameof(Widget.Name), Assert.Single(errors).Field);
#else
		var exception = Assert.Throws<DomainValidationException>(() => Widget.Create(name));

		Assert.Equal(nameof(Widget.Name), Assert.Single(exception.Errors).Field);
#endif
	}

	[Fact]
	public void UpdateName_changes_the_name()
	{
#if (result)
		var entity = Widget.Create("First").ShouldSucceedWithValue();

		entity.UpdateName("Second").ShouldSucceed();
#else
		var entity = Widget.Create("First");

		entity.UpdateName("Second");
#endif

		Assert.Equal("Second", entity.Name);
	}

	[Fact]
	public void UpdateName_with_an_invalid_name_keeps_the_old_one()
	{
#if (result)
		var entity = Widget.Create("First").ShouldSucceedWithValue();

		entity.UpdateName("").ShouldFailValidation();
#else
		var entity = Widget.Create("First");

		Assert.Throws<DomainValidationException>(() => entity.UpdateName(""));
#endif

		Assert.Equal("First", entity.Name);
	}

	[Fact]
	public void Delete_raises_deleted()
	{
#if (result)
		var entity = Widget.Create("First").ShouldSucceedWithValue();
#else
		var entity = Widget.Create("First");
#endif
		entity.ClearDomainEvents();

		entity.Delete();

		var deleted = Assert.IsType<WidgetDeletedDomainEvent>(Assert.Single(entity.DomainEvents));
		Assert.Equal(entity.Id, deleted.Id);
	}
}
