using System.Diagnostics;

namespace CleanSlices.Matrix;

/// <summary>
/// Runs one job against the template repository: the CLI built from source generates the solution, the solution's
/// own tests run, and (for one job per symbol set) the template repository is tested with the job's options.
/// Every step streams its output; the first failing step fails the job.
/// </summary>
internal sealed class JobRunner(string repositoryRoot, string templateRef, string workDirectory)
{
    private const string SolutionName = "Acme.Matrix";

    private static readonly bool OnGitHubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

    private string? _cli;

    public async Task<bool> RunAsync(MatrixJob job, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Job {job.Id}: {job.Describe()}");
        Console.WriteLine($"Template: {repositoryRoot} at {templateRef}; work folder: {workDirectory}");

        if (Directory.Exists(workDirectory))
        {
            DeleteDirectory(workDirectory);
        }

        Directory.CreateDirectory(workDirectory);

        if (!await StepAsync("Build the CLI", repositoryRoot, "dotnet", ["build", "cli/CleanSlices.Cli", "-c", "Release", "-v", "q", "-nologo"], cancellationToken))
        {
            return false;
        }

        _cli = Path.Combine(repositoryRoot, "cli", "CleanSlices.Cli", "bin", "Release", "net10.0", "CleanSlices.Cli.dll");

        if (job.RepositoryVariant)
        {
            // %3B: a plain ';' or ',' would split the -p value.
            var symbols = string.Join("%3B", job.Symbols);
            if (!await StepAsync($"Template repository with {string.Join(", ", job.Symbols)}", repositoryRoot, "dotnet",
                    ["test", "--solution", "CleanSlices.Dev.slnx", $"-p:TemplateOptions={symbols}"], cancellationToken))
            {
                return false;
            }
        }

        var solution = Path.Combine(workDirectory, SolutionName);
        string[] samples = job.Samples ? ["--samples"] : ["--no-samples"];
        string[] with = [.. job.Options.SelectMany(o => new[] { "--with", o.ToString() })];
        var specs = job.Options.Select(o => o.ToString()).ToArray();

        var steps = job.Flow == Flow.New
            ? new (string Name, string Directory, string File, string[] Arguments)[]
            {
                ("cleanslices new", workDirectory, "dotnet", [_cli, "new", SolutionName, .. samples, .. with, "-y"]),
                ("cleanslices add feature", solution, "dotnet", [_cli, "add", "feature", "Product", "-y"]),
                ("dotnet test", solution, "dotnet", ["test"]),
            }
            :
            [
                ("cleanslices new", workDirectory, "dotnet", [_cli, "new", SolutionName, .. samples, "-y"]),
                ("cleanslices add feature", solution, "dotnet", [_cli, "add", "feature", "Product", "-y"]),
                ("git add", solution, "git", ["add", "-A"]),
                ("git commit", solution, "git", ["commit", "-q", "-m", "feat: Product"]),
                ($"cleanslices add {string.Join(" ", specs)}", solution, "dotnet", [_cli, "add", .. specs, "-y"]),
                ("dotnet test", solution, "dotnet", ["test"]),
            ];

        foreach (var (name, directory, file, arguments) in steps)
        {
            if (!await StepAsync(name, directory, file, arguments, cancellationToken))
            {
                return false;
            }
        }

        Console.WriteLine($"Job {job.Id} passed. The solution is in {solution}.");
        return true;
    }

    private async Task<bool> StepAsync(string name, string directory, string file, string[] arguments, CancellationToken cancellationToken)
    {
        Console.WriteLine(OnGitHubActions ? $"::group::{name}" : $"--- {name}");
        var stopwatch = Stopwatch.StartNew();

        var startInfo = new ProcessStartInfo(file) { WorkingDirectory = directory, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["CLEANSLICES_TEMPLATE_REPO"] = repositoryRoot;
        startInfo.Environment["CLEANSLICES_TEMPLATE_REF"] = templateRef;
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        // The CLI commits (initial commit, `add <option>`); CI machines have no git identity.
        startInfo.Environment["GIT_AUTHOR_NAME"] = "cleanslices matrix";
        startInfo.Environment["GIT_AUTHOR_EMAIL"] = "matrix@cleanslices.invalid";
        startInfo.Environment["GIT_COMMITTER_NAME"] = "cleanslices matrix";
        startInfo.Environment["GIT_COMMITTER_EMAIL"] = "matrix@cleanslices.invalid";

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Couldn't start {file}.");
        await process.WaitForExitAsync(cancellationToken);

        if (OnGitHubActions)
        {
            Console.WriteLine("::endgroup::");
        }

        if (process.ExitCode != 0)
        {
            var message = $"Step '{name}' failed (exit code {process.ExitCode}).";
            Console.WriteLine(OnGitHubActions ? $"::error title={name}::{message}" : $"FAILED: {message}");
            return false;
        }

        Console.WriteLine($"    {name}: ok ({stopwatch.Elapsed.TotalSeconds:0}s)");
        return true;
    }

    private static void DeleteDirectory(string path)
    {
        // Git marks object files read-only, which makes Directory.Delete fail on Windows.
        foreach (var entry in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(entry, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
