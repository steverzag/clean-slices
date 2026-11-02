using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace CleanSlices.Cli.Infrastructure;

internal sealed record ProcessResult(int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;
}

internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default)
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

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        var gate = new Lock();

        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);

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
            return new ProcessResult(process.ExitCode, output.ToString());
        }

        void Append(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (gate)
            {
                output.AppendLine(line);
            }
        }
    }
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
