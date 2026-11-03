using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace CleanSlices.Cli.Infrastructure;

/// <summary><see cref="Output"/> interleaves stdout and stderr for display; <see cref="StandardOutput"/> is stdout only, for parsing.</summary>
internal sealed record ProcessResult(int ExitCode, string Output, string StandardOutput = "")
{
    public bool Succeeded => ExitCode == 0;
}

internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        foreach (var (key, value) in environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[key] = value;
        }

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        var standardOutput = new StringBuilder();
        var gate = new Lock();

        process.OutputDataReceived += (_, e) => Append(e.Data, isStandardOutput: true);
        process.ErrorDataReceived += (_, e) => Append(e.Data, isStandardOutput: false);

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            return new ProcessResult(-1, $"Could not start '{fileName}': {ex.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        lock (gate)
        {
            return new ProcessResult(process.ExitCode, output.ToString(), standardOutput.ToString());
        }

        void Append(string? line, bool isStandardOutput)
        {
            if (line is null)
            {
                return;
            }

            lock (gate)
            {
                output.AppendLine(line);
                if (isStandardOutput)
                {
                    standardOutput.AppendLine(line);
                }
            }
        }
    }
}

/// <summary>A git/dotnet step failed; <see cref="Output"/> holds the process output for display.</summary>
internal sealed class ToolException(string message, string output) : Exception(message)
{
    public string Output { get; } = output;
}

internal static class Dotnet
{
    public static Task<ProcessResult> RunAsync(string workingDirectory, IEnumerable<string> arguments, CancellationToken cancellationToken) =>
        ProcessRunner.RunAsync("dotnet", arguments, workingDirectory, cancellationToken);

    public static async Task<bool> IsTemplateInstalledAsync(string shortName, CancellationToken cancellationToken)
    {
        // `dotnet new list <name>` exits with 0 when a match exists and 103 when it doesn't.
        var result = await RunAsync(Environment.CurrentDirectory, ["new", "list", shortName], cancellationToken);
        return result.Succeeded;
    }

    public static async Task<bool> IsEfToolAvailableAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        var result = await RunAsync(workingDirectory, ["ef", "--version"], cancellationToken);
        return result.Succeeded;
    }
}

internal static class Git
{
    public static Task<ProcessResult> RunAsync(
        string workingDirectory,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null) =>
        ProcessRunner.RunAsync("git", ["-c", "core.longpaths=true", .. arguments], workingDirectory, cancellationToken, environment);

    /// <summary>Runs git and returns trimmed stdout, throwing <see cref="ToolException"/> on failure.</summary>
    public static async Task<string> RunOrThrowAsync(
        string workingDirectory,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var argumentList = arguments.ToList();
        var result = await RunAsync(workingDirectory, argumentList, cancellationToken, environment);
        if (!result.Succeeded)
        {
            throw new ToolException($"git {string.Join(' ', argumentList)} failed", result.Output);
        }

        return result.StandardOutput.Trim();
    }

    public static async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync("git", ["--version"], Environment.CurrentDirectory, cancellationToken);
        return result.Succeeded;
    }

    /// <summary>
    /// Initializes a repository and commits the freshly generated solution.
    /// That first commit is the pristine template output, useful later as a merge base.
    /// </summary>
    public static async Task<ProcessResult> InitAndCommitAsync(string root, string message, CancellationToken cancellationToken)
    {
        string[][] steps =
        [
            ["init", "-b", "main"],
            ["add", "-A"],
            ["commit", "-m", message],
        ];

        ProcessResult result = new(0, string.Empty);
        foreach (var step in steps)
        {
            result = await ProcessRunner.RunAsync("git", step, root, cancellationToken);
            if (!result.Succeeded)
            {
                return result with { Output = $"git {string.Join(' ', step)} failed:{Environment.NewLine}{result.Output}" };
            }
        }

        return result;
    }
}
