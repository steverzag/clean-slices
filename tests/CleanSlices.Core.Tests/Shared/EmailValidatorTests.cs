using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Tests.Shared;

public class EmailValidatorTests
{
	[Theory]
	[InlineData("ana@example.com")]
	[InlineData("ana.lopez+news@mail.example.org")]
	[InlineData("o'brien@example.co.uk")]
	public void Valid_addresses_are_accepted(string email)
	{
		Assert.True(EmailValidator.IsValid(email));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("ana")]
	[InlineData("@example.com")]
	[InlineData("ana@")]
	[InlineData("ana@example")]
	[InlineData("ana..lopez@example.com")]
	[InlineData("ana lopez@example.com")]
	[InlineData("ana@-example.com")]
	public void Invalid_addresses_are_rejected(string? email)
	{
		Assert.False(EmailValidator.IsValid(email));
	}

	[Fact]
	public void Local_part_longer_than_the_limit_is_rejected()
	{
		var email = new string('a', EmailValidator.LocalPartMaxLength + 1) + "@example.com";

		Assert.False(EmailValidator.IsValid(email));
	}
}
