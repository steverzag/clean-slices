using System.Formats.Tar;
using System.Text.Json;
using System.Text.Json.Nodes;
using CleanSlices.Cli.Catalog;

namespace CleanSlices.Cli.Infrastructure;

/// <summary>
/// A throwaway clone of the template repository. A <see cref="TemplateComposition"/> is one template commit,
/// rendered through an isolated template hive so the user's installed templates are never touched.
/// Options are template parameters; which ones exist is in the commit's <c>catalog.json</c>.
/// Disposing deletes everything.
/// </summary>
internal sealed class TemplateRepository : IDisposable
{
    public const string EnvironmentVariable = "CLEANSLICES_TEMPLATE_REPO";

    /// <summary>Used when nothing more specific is configured (option, solution manifest, environment variable).</summary>
    public const string DefaultSource = "https://github.com/steverzag/clean-slices.git";

    private const string MainBranch = "origin/main";

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

    /// <summary>Short temp directory (Windows path limits) for clones and renders.</summary>
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

    /// <summary>
    /// The catalog at <paramref name="templateCommit"/>, or null for commits from before options existed
    /// (legacy: rendered without option parameters, and no options can be added).
    /// </summary>
    /// <exception cref="ToolException">The catalog exists but is invalid or newer than this CLI.</exception>
    public async Task<OptionCatalog?> ReadCatalogAsync(string templateCommit, CancellationToken cancellationToken)
    {
        var show = await Git.RunAsync(_clone, ["show", $"{templateCommit}:{OptionCatalog.FileName}"], cancellationToken);
        if (!show.Succeeded)
        {
            return null;
        }

        OptionCatalog catalog;
        try
        {
            catalog = OptionCatalog.Parse(show.StandardOutput);
        }
        catch (CatalogException ex)
        {
            throw new ToolException(ex.Message, string.Empty);
        }

        var errors = CatalogValidator.Validate(catalog);
        return errors.Count == 0
            ? catalog
            : throw new ToolException($"The template's {OptionCatalog.FileName} at {templateCommit[..7]} is inconsistent.", string.Join(Environment.NewLine, errors));
    }

    /// <summary>Exports <paramref name="templateCommit"/> for rendering.</summary>
    /// <param name="stableGuids">Keep the template's GUIDs instead of regenerating them (for before/after diff renders).</param>
    public async Task<TemplateComposition> ComposeAsync(string templateCommit, CancellationToken cancellationToken, bool stableGuids = false)
    {
        var id = ++_compositions;

        // An archive rather than a checkout: only tracked files, and no `.git` entry for the template to copy.
        var export = Path.Combine(WorkRoot, $"x{id}");
        var archive = Path.Combine(WorkRoot, $"x{id}.tar");
        await Git.RunOrThrowAsync(_clone, ["archive", "--format=tar", "-o", archive, templateCommit], cancellationToken);
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
    private const string SolutionTemplateJson = ".template.config/template.json";
    private const string FeatureTemplateJson = "templates/cs-feature/.template.config/template.json";

    private bool _installed;

    /// <summary>Renders the solution plus every recorded slice, i.e. what the user got from the CLI (minus their own edits).</summary>
    public async Task RenderProjectAsync(ProjectManifest manifest, IReadOnlyList<(string Name, string Value)> options, string output, CancellationToken cancellationToken)
    {
        await RenderSolutionAsync(manifest.Name, manifest.Samples, options, output, cancellationToken);
        foreach (var slice in manifest.Slices)
        {
            await RenderFeatureAsync(manifest.Name, slice, options, output, cancellationToken);
        }
    }

    public Task RenderSolutionAsync(string name, bool samples, IReadOnlyList<(string Name, string Value)> options, string output, CancellationToken cancellationToken) =>
        RunNewAsync(
            ["cleanslices", "-n", name, "-o", output, .. samples ? new[] { "--samples" } : [], .. Parameters(SolutionTemplateJson, options)],
            cancellationToken);

    public Task RenderFeatureAsync(string app, SliceEntry slice, IReadOnlyList<(string Name, string Value)> options, string output, CancellationToken cancellationToken) =>
        RunNewAsync(
            ["cs-feature", "-n", slice.Name, "--plural", slice.Plural, "--app", app, "-o", output, .. Parameters(FeatureTemplateJson, options)],
            cancellationToken);

    /// <summary>
    /// Option parameters for one template: only those it defines. The feature template declares just the options its
    /// code depends on, and dotnet new rejects parameters a template doesn't have.
    /// </summary>
    private IEnumerable<string> Parameters(string templateJson, IReadOnlyList<(string Name, string Value)> options)
    {
        var path = Path.Combine(directory, templateJson);
        if (options.Count == 0 || !File.Exists(path))
        {
            return [];
        }

        var symbols = JsonNode.Parse(File.ReadAllText(path))?["symbols"]?.AsObject();
        return options
            .Where(o => symbols?[o.Name]?["type"]?.GetValue<string>() == "parameter")
            .SelectMany(o => new[] { $"--{o.Name}", o.Value });
    }

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
