using CleanSlices.Cli.Commands;

namespace CleanSlices.Cli.Tests.Commands;

public class AddOptionArgumentsTests
{
    [Theory]
    [InlineData("add result", "add option result")]
    [InlineData("add result outbox -y", "add option result outbox -y")]
    [InlineData("add cqrs:dispatcher=mediator", "add option cqrs:dispatcher=mediator")]
    [InlineData("add", "add option")]
    [InlineData("add -y", "add option -y")]
    [InlineData("add feature Product", "add feature Product")]
    [InlineData("add option result", "add option result")]
    [InlineData("add --help", "add --help")]
    [InlineData("new Acme.Orders --with result", "new Acme.Orders --with result")]
    [InlineData("list", "list")]
    public void Options_after_add_go_to_the_option_command(string input, string expected)
    {
        Assert.Equal(expected.Split(' '), AddOptionArguments.Rewrite(input.Split(' ')));
    }
}
