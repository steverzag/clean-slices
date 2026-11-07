using CleanSlices.Cli.Commands;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace CleanSlices.Cli.Tests.Commands;

/// <summary>Every command's help renders: descriptions are markup, and an unescaped bracket breaks the whole page.</summary>
public class HelpTests
{
    private static (int ExitCode, string Output) Run(string command)
    {
        using var console = new TestConsole();
        console.Profile.Width = 200;

        var app = new CommandApp<NewCommand>();
        app.Configure(config =>
        {
            CliApp.Configure(config);
            config.ConfigureConsole(console);
            config.PropagateExceptions();
        });

        var exitCode = app.Run(AddOptionArguments.Rewrite(command.Split(' ')));
        return (exitCode, console.Output);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("new --help")]
    [InlineData("list --help")]
    [InlineData("add --help")]
    [InlineData("add feature --help")]
    [InlineData("add option --help")]
    public void Help_renders(string command)
    {
        var (exitCode, output) = Run(command);

        Assert.Equal(0, exitCode);
        Assert.Contains("USAGE", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Brackets_in_descriptions_come_out_as_text()
    {
        // Written [[,...]] in the description; it must come out as plain brackets, not as markup.
        Assert.Contains("<id>:<choice>=<value>[,...]", Run("new --help").Output, StringComparison.Ordinal);
    }
}
