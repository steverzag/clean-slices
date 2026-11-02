using System.Text.Json;

namespace CleanSlices.Cli.Infrastructure;

/// <summary>
/// The answers file (<c>.cleanslices.json</c>) written at the solution root.
/// It records how the solution was generated so later commands (add feature, future pattern merges)
/// can reproduce the same template output without asking again.
/// </summary>
internal sealed record ProjectManifest(
    string Name,
    string CliVersion,
    ProjectOptions Options,
    IReadOnlyList<SliceEntry> Slices)
{
    public const string FileName = ".cleanslices.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static ProjectManifest CreateNew(string name, bool samples) =>
        new(name, CliInfo.Version, new ProjectOptions(samples), []);

    public ProjectManifest WithSlice(SliceEntry slice) =>
        this with { Slices = [.. Slices, slice] };

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
