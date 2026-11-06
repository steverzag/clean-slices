namespace CleanSlices.Cli.Commands;

/// <summary>
/// Spectre treats the word after <c>add</c> as a subcommand, so <c>add result</c> would be "unknown command 'result'".
/// Everything after <c>add</c> that isn't <c>feature</c> or help goes to the hidden <c>add option</c> command,
/// which makes <c>feature</c> the one reserved word.
/// </summary>
internal static class AddOptionArguments
{
    public const string Command = "option";

    private static readonly string[] Reserved = ["feature", Command, "-h", "--help", "-?"];

    public static string[] Rewrite(string[] args) =>
        args is ["add", .. var rest] && (rest.Length == 0 || !Reserved.Contains(rest[0], StringComparer.Ordinal))
            ? ["add", Command, .. rest]
            : args;
}
