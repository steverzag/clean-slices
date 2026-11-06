using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CleanSlices.Cli.Catalog;

namespace CleanSlices.Cli.Infrastructure;

/// <summary>
/// The answers file (<c>.cleanslices.json</c>) written at the solution root.
/// It records how the solution was generated so later commands can re-render exactly the same template output:
/// <c>add feature</c> renders from the same template version and options, and <c>add &lt;option&gt;</c> renders the
/// solution before/after the option to merge the difference into the user's code.
/// </summary>
internal sealed record ProjectManifest
{
    public const string FileName = ".cleanslices.json";

    /// <summary>The newest manifest format this CLI writes and understands.</summary>
    public const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new OptionEntryConverter() },
    };

    public int ManifestVersion { get; init; } = CurrentVersion;

    public required string Name { get; init; }

    public string CliVersion { get; init; } = CliInfo.Version;

    public bool Samples { get; init; }

    /// <summary>Where the templates come from (local path or git URL). Null for solutions made from installed templates only.</summary>
    public string? TemplateRepo { get; init; }

    /// <summary>The template commit (on main) the solution was generated from: the base for every later render.</summary>
    public string? TemplateCommit { get; init; }

    /// <summary>The resolved configuration: every unit applied, options and the hidden and glue units they brought.</summary>
    public IReadOnlyList<OptionEntry> Options { get; init; } = [];

    public IReadOnlyList<SliceEntry> Slices { get; init; } = [];

    public static ProjectManifest CreateNew(string name, bool samples) =>
        new() { Name = name, Samples = samples };

    public ProjectManifest WithSlice(SliceEntry slice) =>
        this with { Slices = [.. Slices, slice] };

    /// <summary>The recorded units as resolver input; the resolver reads only the options and recomputes the rest.</summary>
    [JsonIgnore]
    public IReadOnlyList<OptionRequest> OptionRequests =>
        [.. Options.Select(o => new OptionRequest(o.Id, o.Choices))];

    /// <exception cref="ToolException">The file is invalid, newer than this CLI, or from the branch-based pattern era.</exception>
    public static (ProjectManifest Manifest, string Root)? Find(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, FileName);
            if (File.Exists(path))
            {
                return (Parse(File.ReadAllText(path), path), dir.FullName);
            }
        }

        return null;
    }

    public static ProjectManifest Parse(string json, string path = FileName)
    {
        JsonObject document;
        try
        {
            document = JsonNode.Parse(json)?.AsObject() ?? throw new ToolException($"{path} is empty.", string.Empty);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new ToolException($"{path} isn't valid JSON: {ex.Message}", string.Empty);
        }

        var version = document["manifestVersion"]?.GetValue<int>() ?? 1;
        if (version > CurrentVersion)
        {
            throw new ToolException($"{path} was written by a newer cleanslices (format {version}). Update cleanslices.", string.Empty);
        }

        return version == 1 ? FromVersion1(document, path) : document.Deserialize<ProjectManifest>(JsonOptions)!;
    }

    /// <summary>
    /// Format 1 (cleanslices 0.1–0.2): <c>options</c> was <c>{ "samples": bool }</c> and patterns were git branches.
    /// Without patterns it maps cleanly; with them, this CLI can't render the solution.
    /// </summary>
    private static ProjectManifest FromVersion1(JsonObject document, string path)
    {
        if (document["patterns"] is JsonArray { Count: > 0 } patterns)
        {
            throw new ToolException(
                $"This solution uses the branch-based pattern(s) {string.Join(", ", patterns.Select(p => $"'{p}'"))} from cleanslices 0.2. "
                + "This version can't extend it: use cleanslices 0.2.x, or create the solution again.",
                string.Empty);
        }

        return new ProjectManifest
        {
            Name = document["name"]?.GetValue<string>() ?? throw new ToolException($"{path} has no name.", string.Empty),
            CliVersion = document["cliVersion"]?.GetValue<string>() ?? CliInfo.Version,
            Samples = document["options"]?["samples"]?.GetValue<bool>() ?? false,
            TemplateRepo = document["templateRepo"]?.GetValue<string>(),
            TemplateCommit = document["templateCommit"]?.GetValue<string>(),
            Slices = document["slices"]?.Deserialize<List<SliceEntry>>(JsonOptions) ?? [],
        };
    }

    public Task SaveAsync(string root, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(
            Path.Combine(root, FileName),
            JsonSerializer.Serialize(this with { ManifestVersion = CurrentVersion }, JsonOptions) + Environment.NewLine,
            cancellationToken);
}

internal sealed record SliceEntry(string Name, string Plural);

/// <summary>
/// One applied unit: <c>{ "id": "cqrs", "dispatcher": "mediator" }</c>. Choices sit next to the id;
/// <c>addedBy</c> lists the units a hidden or glue unit was added for.
/// </summary>
internal sealed record OptionEntry(string Id, IReadOnlyDictionary<string, string> Choices, IReadOnlyList<string> AddedBy)
{
    public static OptionEntry From(ResolvedUnit resolved) =>
        new(resolved.Unit.Id, resolved.Choices, resolved.Origin is Origin.Required or Origin.Glue ? resolved.AddedBy : []);
}

internal sealed class OptionEntryConverter : JsonConverter<OptionEntry>
{
    private const string IdProperty = "id";
    private const string AddedByProperty = "addedBy";

    public override OptionEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var node = JsonNode.Parse(ref reader)?.AsObject() ?? throw new JsonException("An option entry must be an object.");
        var id = node[IdProperty]?.GetValue<string>() ?? throw new JsonException("An option entry has no id.");
        var addedBy = node[AddedByProperty]?.AsArray().Select(a => a!.GetValue<string>()).ToList() ?? [];
        var choices = node
            .Where(p => p.Key is not (IdProperty or AddedByProperty))
            .ToDictionary(p => p.Key, p => p.Value!.GetValue<string>(), StringComparer.Ordinal);

        return new OptionEntry(id, choices, addedBy);
    }

    public override void Write(Utf8JsonWriter writer, OptionEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(IdProperty, value.Id);
        foreach (var (choice, selected) in value.Choices)
        {
            writer.WriteString(choice, selected);
        }

        if (value.AddedBy.Count > 0)
        {
            writer.WriteStartArray(AddedByProperty);
            foreach (var id in value.AddedBy)
            {
                writer.WriteStringValue(id);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
