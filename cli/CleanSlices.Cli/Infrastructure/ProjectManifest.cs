using System.Text.Json;
using System.Text.Json.Serialization;

namespace CleanSlices.Cli.Infrastructure;

/// <summary>
/// The answers file (<c>.cleanslices.json</c>) written at the solution root.
/// It records how the solution was generated so later commands can re-render exactly the same template output:
/// <c>add feature</c> renders from the same template version, and <c>add pattern</c> renders the solution
/// before/after the pattern to merge the difference into the user's code.
/// </summary>
internal sealed record ProjectManifest
{
    public const string FileName = ".cleanslices.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public required string Name { get; init; }

    public string CliVersion { get; init; } = CliInfo.Version;

    public ProjectOptions Options { get; init; } = new(Samples: false);

    /// <summary>Where the templates come from (local path or git URL). Null for solutions made from installed templates only.</summary>
    public string? TemplateRepo { get; init; }

    /// <summary>The template commit (on main) the solution was generated from: the base for every later render.</summary>
    public string? TemplateCommit { get; init; }

    /// <summary>Applied patterns, in application order (each maps to a <c>pattern/&lt;name&gt;</c> branch).</summary>
    public IReadOnlyList<string> Patterns { get; init; } = [];

    public IReadOnlyList<SliceEntry> Slices { get; init; } = [];

    public static ProjectManifest CreateNew(string name, bool samples) =>
        new() { Name = name, Options = new ProjectOptions(samples) };

    public ProjectManifest WithSlice(SliceEntry slice) =>
        this with { Slices = [.. Slices, slice] };

    public ProjectManifest WithPattern(string pattern) =>
        this with { Patterns = [.. Patterns, pattern] };

    public static (ProjectManifest Manifest, string Root)? Find(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, FileName);
            if (!File.Exists(path))
            {
                continue;
            }

            var manifest = JsonSerializer.Deserialize<ProjectManifest>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidOperationException($"{path} is empty or invalid.");

            return (manifest, dir.FullName);
        }

        return null;
    }

    public Task SaveAsync(string root, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(
            Path.Combine(root, FileName),
            JsonSerializer.Serialize(this, JsonOptions) + Environment.NewLine,
            cancellationToken);
}

internal sealed record ProjectOptions(bool Samples);

internal sealed record SliceEntry(string Name, string Plural);
