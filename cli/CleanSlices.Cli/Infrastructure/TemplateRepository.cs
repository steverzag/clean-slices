using System.Formats.Tar;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CleanSlices.Cli.Infrastructure;

internal sealed record PatternInfo(string Name, string Description);

/// <summary>
/// A throwaway clone of the template repository. Patterns live on <c>pattern/&lt;name&gt;</c> branches based on
/// <c>main</c>; a <see cref="TemplateComposition"/> is a template commit with pattern branches cherry-picked on top,
/// rendered through an isolated template hive so the user's installed templates are never touched.
/// Disposing deletes everything.
/// </summary>
internal sealed class TemplateRepository : IDisposable
{
    public const string EnvironmentVariable = "CLEANSLICES_TEMPLATE_REPO";

    /// <summary>Used when nothing more specific is configured (option, solution manifest, environment variable).</summary>
    public const string DefaultSource = "https://github.com/steverzag/clean-slices.git";

    private const string MainBranch = "origin/main";
    private const string PatternBranchPrefix = "pattern/";

    // Synthetic commits in the throwaway clone shouldn't depend on the user's git identity.
    private static readonly string[] CommitIdentity = ["-c", "user.name=cleanslices", "-c", "user.email=cli@cleanslices.invalid"];

    private readonly string _clone;
    private int _compositions;

    private TemplateRepository(string source, string workRoot)
    {
        Source = source;
        WorkRoot = workRoot;
        _clone = Path.Combine(workRoot, "repo");
    }

    /// <summary>Normalized source: an absolute path for local repositories, otherwise the URL as given.</summary>
    public string Source { get; }

    /// <summary>Short temp directory (Windows path limits) for clones, worktrees and renders.</summary>
    public string WorkRoot { get; }

    /// <summary>
    /// Precedence: explicit option → the repository recorded in the solution's manifest →
    /// <see cref="EnvironmentVariable"/> (e.g. a local working copy while developing templates) → <see cref="DefaultSource"/>.
    /// </summary>
    public static string ResolveSource(string? explicitSource, ProjectManifest? manifest)
    {
        var source = new[] { explicitSource, manifest?.TemplateRepo, Environment.GetEnvironmentVariable(EnvironmentVariable) }
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
            ?? DefaultSource;

        return Directory.Exists(source) ? Path.GetFullPath(source) : source;
    }

    public static bool IsDefaultSource(string source) =>
        string.Equals(source, DefaultSource, StringComparison.OrdinalIgnoreCase);

    public static async Task<TemplateRepository> OpenAsync(string source, CancellationToken cancellationToken)
    {
        var workRoot = Path.Combine(Path.GetTempPath(), "cleanslices", Path.GetRandomFileName().Replace(".", string.Empty, StringComparison.Ordinal)[..8]);
        Directory.CreateDirectory(workRoot);

        var repository = new TemplateRepository(source, workRoot);
        try
        {
            // Remote clones are partial (file contents fetched on demand), which keeps them small as history grows.
            string[] filter = Directory.Exists(source) ? [] : ["--filter=blob:none"];
            var clone = await Git.RunAsync(workRoot, ["clone", "--quiet", "--no-checkout", .. filter, source, "repo"], cancellationToken);
            if (!clone.Succeeded)
            {
                throw new ToolException($"Couldn't fetch the template repository {source}.", clone.Output);
            }

            var main = await Git.RunAsync(repository._clone, ["rev-parse", "--verify", MainBranch], cancellationToken);
            if (!main.Succeeded)
            {
                throw new ToolException($"The template repository {source} has no 'main' branch.", main.Output);
            }
        }
        catch
        {
            repository.Dispose();
            throw;
        }

        return repository;
    }

    public Task<string> GetMainCommitAsync(CancellationToken cancellationToken) =>
        Git.RunOrThrowAsync(_clone, ["rev-parse", MainBranch], cancellationToken);

    public async Task<IReadOnlyList<PatternInfo>> ListPatternsAsync(CancellationToken cancellationToken)
    {
        // refs/remotes/origin/pattern/<name> → "<name>"
        var output = await Git.RunOrThrowAsync(
            _clone,
            ["for-each-ref", "--format=%(refname:lstrip=4)", $"refs/remotes/origin/{PatternBranchPrefix}"],
            cancellationToken);

        var patterns = new List<PatternInfo>();
        foreach (var name in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // The description is the subject of the pattern's *first* commit, so follow-up commits
            // (docs, fixes) on the branch don't change how the pattern is presented.
            var branch = $"origin/{PatternBranchPrefix}{name}";
            var forkPoint = await Git.RunOrThrowAsync(_clone, ["merge-base", MainBranch, branch], cancellationToken);
            var subjects = await Git.RunOrThrowAsync(_clone, ["log", "--reverse", "--format=%s", $"{forkPoint}..{branch}"], cancellationToken);
            patterns.Add(new PatternInfo(name, CleanSubject(subjects.Split('\n')[0].Trim())));
        }

        return patterns.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();

        static string CleanSubject(string subject)
        {
            var separator = subject.IndexOf("): ", StringComparison.Ordinal);
            return separator >= 0 ? subject[(separator + 3)..] : subject;
        }
    }

    /// <summary>Checks out <paramref name="templateCommit"/> and cherry-picks each pattern branch's own commits on top, in order.</summary>
    /// <param name="stableGuids">Keep the template's GUIDs instead of regenerating them (for before/after diff renders).</param>
    public async Task<TemplateComposition> ComposeAsync(
        string templateCommit,
        IReadOnlyList<string> patterns,
        CancellationToken cancellationToken,
        bool stableGuids = false)
    {
        var id = ++_compositions;
        var worktree = Path.Combine(WorkRoot, $"t{id}");
        await Git.RunOrThrowAsync(_clone, ["worktree", "add", "--quiet", "--detach", worktree, templateCommit], cancellationToken);

        foreach (var pattern in patterns)
        {
            var branch = $"origin/{PatternBranchPrefix}{pattern}";

            // Only the pattern's own commits, even if main moved on since the solution was generated.
            var forkPoint = await Git.RunOrThrowAsync(worktree, ["merge-base", MainBranch, branch], cancellationToken);
            var pick = await Git.RunAsync(worktree, [.. CommitIdentity, "cherry-pick", "--allow-empty", $"{forkPoint}..{branch}"], cancellationToken);

            if (!pick.Succeeded)
            {
                throw new ToolException(
                    $"Pattern '{pattern}' doesn't apply cleanly on top of template {templateCommit[..7]}"
                    + (patterns.Count > 1 ? $" combined with {string.Join(", ", patterns.TakeWhile(p => p != pattern))}." : "."),
                    pick.Output);
            }
        }

        // Render from a clean export, not the worktree: a worktree has a `.git` *file*, which the template
        // doesn't exclude (it only excludes the `.git/` directory) and would be copied into the output.
        var export = Path.Combine(WorkRoot, $"x{id}");
        var archive = Path.Combine(WorkRoot, $"x{id}.tar");
        await Git.RunOrThrowAsync(worktree, ["archive", "--format=tar", "-o", archive, "HEAD"], cancellationToken);
        Directory.CreateDirectory(export);
        await TarFile.ExtractToDirectoryAsync(archive, export, overwriteFiles: false, cancellationToken);

        if (stableGuids)
        {
            DisableGuidRegeneration(Path.Combine(export, ".template.config", "template.json"));
        }

        return new TemplateComposition(export, Path.Combine(WorkRoot, $"h{id}"));
    }

    /// <summary>
    /// The template regenerates project GUIDs (solution ids, UserSecretsId) on every render. For before/after renders
    /// that would make every such line differ three ways and conflict; keeping the template's own GUIDs in both renders
    /// makes those lines identical, so the merge keeps the user's values.
    /// </summary>
    private static void DisableGuidRegeneration(string templateJsonPath)
    {
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var template = JsonNode.Parse(File.ReadAllText(templateJsonPath), documentOptions: options)!.AsObject();
        if (template.Remove("guids"))
        {
            File.WriteAllText(templateJsonPath, template.ToJsonString());
        }
    }

    public void Dispose()
    {
        if (!Directory.Exists(WorkRoot))
        {
            return;
        }

        try
        {
            // Git marks object files read-only, which makes Directory.Delete fail on Windows.
            foreach (var file in Directory.EnumerateFiles(WorkRoot, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(WorkRoot, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: it's a temp directory.
        }
    }
}

/// <summary>A checked-out template version, renderable via its own template hive.</summary>
internal sealed class TemplateComposition(string directory, string hive)
{
    private bool _installed;

    /// <summary>Renders the solution plus every recorded slice, i.e. what the user got from the CLI (minus their own edits).</summary>
    public async Task RenderProjectAsync(ProjectManifest manifest, string output, CancellationToken cancellationToken)
    {
        await RenderSolutionAsync(manifest.Name, manifest.Options.Samples, output, cancellationToken);
        foreach (var slice in manifest.Slices)
        {
            await RenderFeatureAsync(manifest.Name, slice, output, cancellationToken);
        }
    }

    public Task RenderSolutionAsync(string name, bool samples, string output, CancellationToken cancellationToken) =>
        RunNewAsync(
            ["cleanslices", "-n", name, "-o", output, .. samples ? new[] { "--samples" } : []],
            cancellationToken);

    public Task RenderFeatureAsync(string app, SliceEntry slice, string output, CancellationToken cancellationToken) =>
        RunNewAsync(["cs-feature", "-n", slice.Name, "--plural", slice.Plural, "--app", app, "-o", output], cancellationToken);

    private async Task RunNewAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var workingDirectory = Path.GetDirectoryName(directory)!;

        if (!_installed)
        {
            var install = await Dotnet.RunAsync(workingDirectory, ["new", "install", directory, "--debug:custom-hive", hive], cancellationToken);
            if (!install.Succeeded)
            {
                throw new ToolException("Installing the template into an isolated hive failed.", install.Output);
            }

            _installed = true;
        }

        var result = await Dotnet.RunAsync(workingDirectory, ["new", .. arguments, "--debug:custom-hive", hive], cancellationToken);
        if (!result.Succeeded)
        {
            throw new ToolException($"dotnet new {arguments[0]} failed.", result.Output);
        }
    }
}
