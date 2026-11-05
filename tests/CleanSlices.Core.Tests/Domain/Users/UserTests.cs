using CleanSlices.Core.Domain.Users;
using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Tests.Domain.Users;

public class UserTests
{
	[Fact]
	public void Create_with_valid_input_sets_the_values_and_raises_created()
	{
		var user = User.Create("Ana", "ana@example.com");

		Assert.Equal("Ana", user.Name);
		Assert.Equal("ana@example.com", user.Email);
		var created = Assert.IsType<UserCreatedDomainEvent>(Assert.Single(user.DomainEvents));
		Assert.Equal(user.Id, created.Id);
	}

	[Fact]
	public void Create_reports_every_invalid_field()
	{
		var exception = Assert.Throws<DomainValidationException>(() => User.Create("", "not-an-email"));

		Assert.Contains(exception.Errors, error => error.Field == nameof(User.Name));
		Assert.Contains(exception.Errors, error => error.Field == nameof(User.Email));
	}

	[Fact]
	public void Create_rejects_a_name_that_is_too_long()
	{
		var name = new string('a', User.NameMaxLength + 1);

		var exception = Assert.Throws<DomainValidationException>(() => User.Create(name, "ana@example.com"));

		Assert.Equal(nameof(User.Name), Assert.Single(exception.Errors).Field);
	}

	[Fact]
	public void UpdateEmail_rejects_an_invalid_address_and_keeps_the_old_one()
	{
		var user = User.Create("Ana", "ana@example.com");

		Assert.Throws<DomainValidationException>(() => user.UpdateEmail("ana@"));

		Assert.Equal("ana@example.com", user.Email);
	}

	[Fact]
	public void Delete_raises_deleted()
	{
		var user = User.Create("Ana", "ana@example.com");
		user.ClearDomainEvents();

		user.Delete();

		var deleted = Assert.IsType<UserDeletedDomainEvent>(Assert.Single(user.DomainEvents));
		Assert.Equal(user.Id, deleted.Id);
	}
}
