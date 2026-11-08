using CleanSlices.Core.Domain.Users;
#if (!result)
using CleanSlices.Core.Shared;
#endif

namespace CleanSlices.Core.Tests.Domain.Users;

public class UserTests
{
	[Fact]
	public void Create_with_valid_input_sets_the_values_and_raises_created()
	{
#if (result)
		var user = User.Create("Ana", "ana@example.com").ShouldSucceedWithValue();
#else
		var user = User.Create("Ana", "ana@example.com");
#endif

		Assert.Equal("Ana", user.Name);
		Assert.Equal("ana@example.com", user.Email);
		var created = Assert.IsType<UserCreatedDomainEvent>(Assert.Single(user.DomainEvents));
		Assert.Equal(user.Id, created.Id);
	}

	[Fact]
	public void Create_reports_every_invalid_field()
	{
#if (result)
		var errors = User.Create("", "not-an-email").ShouldFailValidation();

		Assert.Contains(errors, error => error.Field == nameof(User.Name));
		Assert.Contains(errors, error => error.Field == nameof(User.Email));
#else
		var exception = Assert.Throws<DomainValidationException>(() => User.Create("", "not-an-email"));

		Assert.Contains(exception.Errors, error => error.Field == nameof(User.Name));
		Assert.Contains(exception.Errors, error => error.Field == nameof(User.Email));
#endif
	}

	[Fact]
	public void Create_rejects_a_name_that_is_too_long()
	{
		var name = new string('a', User.NameMaxLength + 1);

#if (result)
		var errors = User.Create(name, "ana@example.com").ShouldFailValidation();

		Assert.Equal(nameof(User.Name), Assert.Single(errors).Field);
#else
		var exception = Assert.Throws<DomainValidationException>(() => User.Create(name, "ana@example.com"));

		Assert.Equal(nameof(User.Name), Assert.Single(exception.Errors).Field);
#endif
	}

	[Fact]
	public void UpdateEmail_rejects_an_invalid_address_and_keeps_the_old_one()
	{
#if (result)
		var user = User.Create("Ana", "ana@example.com").ShouldSucceedWithValue();

		user.UpdateEmail("ana@").ShouldFailValidation();
#else
		var user = User.Create("Ana", "ana@example.com");

		Assert.Throws<DomainValidationException>(() => user.UpdateEmail("ana@"));
#endif

		Assert.Equal("ana@example.com", user.Email);
	}

	[Fact]
	public void Updating_the_name_or_the_email_raises_updated()
	{
#if (result)
		var user = User.Create("Ana", "ana@example.com").ShouldSucceedWithValue();
		user.ClearDomainEvents();

		user.UpdateName("Ana María").ShouldSucceed();
		user.UpdateEmail("ana.maria@example.com").ShouldSucceed();
#else
		var user = User.Create("Ana", "ana@example.com");
		user.ClearDomainEvents();

		user.UpdateName("Ana María");
		user.UpdateEmail("ana.maria@example.com");
#endif

		Assert.All(user.DomainEvents, e => Assert.Equal(user.Id, Assert.IsType<UserUpdatedDomainEvent>(e).Id));
		Assert.Equal(2, user.DomainEvents.Count);
	}

	[Fact]
	public void A_rejected_update_raises_nothing()
	{
#if (result)
		var user = User.Create("Ana", "ana@example.com").ShouldSucceedWithValue();
		user.ClearDomainEvents();

		user.UpdateName("").ShouldFailValidation();
#else
		var user = User.Create("Ana", "ana@example.com");
		user.ClearDomainEvents();

		Assert.Throws<DomainValidationException>(() => user.UpdateName(""));
#endif

		Assert.Empty(user.DomainEvents);
	}

	[Fact]
	public void Delete_raises_deleted()
	{
#if (result)
		var user = User.Create("Ana", "ana@example.com").ShouldSucceedWithValue();
#else
		var user = User.Create("Ana", "ana@example.com");
#endif
		user.ClearDomainEvents();

		user.Delete();

		var deleted = Assert.IsType<UserDeletedDomainEvent>(Assert.Single(user.DomainEvents));
		Assert.Equal(user.Id, deleted.Id);
	}
}
