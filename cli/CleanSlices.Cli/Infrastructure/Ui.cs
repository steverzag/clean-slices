using Spectre.Console;

namespace CleanSlices.Cli.Infrastructure;

internal static class Ui
{
    public static void Header(string subtitle)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold mediumpurple]clean[/][bold]slices[/] [grey]v{CliInfo.Version} · {Markup.Escape(subtitle)}[/]");
        AnsiConsole.WriteLine();
    }

    public static void Success(string message) =>
        AnsiConsole.MarkupLine($"[green]✔[/] {Markup.Escape(message)}");

    public static void Warning(string message) =>
        AnsiConsole.MarkupLine($"[yellow]![/] {Markup.Escape(message)}");

    public static void Error(string message) =>
        AnsiConsole.MarkupLine($"[red]✖[/] {Markup.Escape(message)}");

    public static void Hint(string message) =>
        AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(message)}[/]");

    public static void Failure(string message, string processOutput)
    {
        Error(message);
        if (!string.IsNullOrWhiteSpace(processOutput))
        {
            AnsiConsole.Write(new Panel(new Text(processOutput.Trim()))
                .Header("[grey]output[/]")
                .BorderColor(Color.Grey));
        }
    }

    public static void NextSteps(params string[] commands)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Next steps[/]");
        foreach (var command in commands)
        {
            AnsiConsole.MarkupLine($"  [aqua]{Markup.Escape(command)}[/]");
        }

        AnsiConsole.WriteLine();
    }

    public static bool IsInteractive(bool acceptDefaults) =>
        !acceptDefaults && AnsiConsole.Profile.Capabilities.Interactive;

    public static string AskValidated(string prompt, Func<string, string?> validate, string? defaultValue = null)
    {
        var textPrompt = new TextPrompt<string>(prompt)
            .Validate(value => validate(value) is { } error
                ? ValidationResult.Error($"[red]{Markup.Escape(error)}[/]")
                : ValidationResult.Success());

        if (defaultValue is not null)
        {
            textPrompt.DefaultValue(defaultValue);
        }

        return AnsiConsole.Prompt(textPrompt);
    }

    public static async Task<bool> EnsureTemplateInstalledAsync(string shortName, CancellationToken cancellationToken)
    {
        if (await Dotnet.IsTemplateInstalledAsync(shortName, cancellationToken))
        {
            return true;
        }

        Error($"The '{shortName}' dotnet new template is not installed.");
        Hint("Install the Clean Slices templates from the template repository root:");
        Hint("  dotnet new install <path-to-clean-slices-repo>");
        return false;
    }
}
