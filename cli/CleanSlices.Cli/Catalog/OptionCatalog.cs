using System.Text.Json;
using System.Text.Json.Serialization;

namespace CleanSlices.Cli.Catalog;

/// <summary>
/// <c>catalog.json</c> in the template repository: what options exist and how they relate.
/// Rendering (template symbols, file excludes) lives in the <c>template.json</c> files.
/// Dictionaries keep the file's order, which is the order of prompts and of <c>cleanslices list</c>.
/// </summary>
internal sealed record OptionCatalog
{
    public const string FileName = "catalog.json";

    /// <summary>The newest format this CLI understands. Older formats must keep loading.</summary>
    public const int SupportedVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public int CatalogVersion { get; init; }

    public OrderedDictionary<string, Concept> Concepts { get; init; } = [];

    public OrderedDictionary<string, Group> Groups { get; init; } = [];

    public IReadOnlyList<Unit> Units { get; init; } = [];

    public Unit? Find(string id) => Units.FirstOrDefault(u => u.Id == id);

    /// <exception cref="CatalogException">The file is invalid or newer than this CLI.</exception>
    public static OptionCatalog Parse(string json)
    {
        int version;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            version = document.RootElement.TryGetProperty("catalogVersion", out var v) && v.TryGetInt32(out var n) ? n : 0;
        }
        catch (JsonException ex)
        {
            throw new CatalogException($"{FileName} is not valid JSON: {ex.Message}");
        }

        // Checked before deserializing: a newer format may not fit this model at all.
        if (version > SupportedVersion)
        {
            throw new CatalogException(
                $"The template uses catalog format {version}, but this cleanslices only understands up to {SupportedVersion}. Update cleanslices.");
        }

        if (version < 1)
        {
            throw new CatalogException($"{FileName} has no valid catalogVersion.");
        }

        try
        {
            return JsonSerializer.Deserialize<OptionCatalog>(json, JsonOptions) ?? throw new CatalogException($"{FileName} is empty.");
        }
        catch (JsonException ex)
        {
            throw new CatalogException($"{FileName} doesn't match the catalog format: {ex.Message}");
        }
    }
}

internal enum Selection
{
    Many,
    OnePerGroup,
}

internal sealed record Concept(string Title, Selection Select);

internal sealed record Group(string Title, bool Required, string? Default);

internal enum UnitKind
{
    Option,
    Hidden,
    Glue,
}

internal sealed record License(string Notice, string? Setting);

internal sealed record ChoiceValue(string Title, string? Description, License? License);

internal sealed record Choice(string Default, OrderedDictionary<string, ChoiceValue> Values);

internal sealed record Unit
{
    public required string Id { get; init; }

    public UnitKind Kind { get; init; }

    public string? Concept { get; init; }

    public string? Group { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public OrderedDictionary<string, Choice> Choices { get; init; } = [];

    public IReadOnlyList<string> Requires { get; init; } = [];

    public IReadOnlyList<string> Provides { get; init; } = [];

    public IReadOnlyList<string> Conflicts { get; init; } = [];

    /// <summary>Whether the sample features support this unit.</summary>
    public bool Samples { get; init; } = true;

    public License? License { get; init; }

    /// <summary>Offered (never added silently) when the selection meets this option's requirements.</summary>
    public bool Recommended { get; init; }

    /// <summary>The unit adds database tables, so adding it to a solution needs a migration.</summary>
    public bool AddsTables { get; init; }

    /// <summary>Glue units: applied when every listed unit is selected.</summary>
    public IReadOnlyList<string> When { get; init; } = [];

    /// <summary>The template symbol: the id as a C# identifier (<c>integration-events</c> → <c>integration_events</c>).</summary>
    public string Symbol => ToSymbol(Id);

    /// <summary>Every unit provides its own id, plus what it declares.</summary>
    public IEnumerable<string> Capabilities => [Id, .. Provides];

    public string DisplayName => Title ?? Id;

    /// <summary>The template parameter that holds the value of one of this unit's choices.</summary>
    public string ChoiceParameter(string choice) => $"{Symbol}_{choice}";

    public static string ToSymbol(string id) => id.Replace('-', '_');
}

internal sealed class CatalogException(string message) : Exception(message);
