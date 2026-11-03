using System.Text.RegularExpressions;

namespace CleanSlices.Core.Shared;

public static partial class EmailValidator
{
	public const int LocalPartMaxLength = 64;

	public static bool IsValid(string? email)
	{
		if (string.IsNullOrWhiteSpace(email))
		{
			return false;
		}

		var at = email.LastIndexOf('@');
		if (at < 1 || at > LocalPartMaxLength)
		{
			return false;
		}

		return EmailPattern().IsMatch(email);
	}

	[GeneratedRegex(
		@"^[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*" +
		@"@([A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,63}$",
		RegexOptions.CultureInvariant)]
	private static partial Regex EmailPattern();
}
