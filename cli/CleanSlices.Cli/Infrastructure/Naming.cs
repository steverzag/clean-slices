using System.Text.RegularExpressions;

namespace CleanSlices.Cli.Infrastructure;

internal static partial class Naming
{
    // Type names that would clash with BCL / solution types once generated (e.g. `Task` vs System.Threading.Tasks.Task).
    private static readonly HashSet<string> ReservedEntityNames = new(StringComparer.Ordinal)
    {
        "Action", "Array", "Attribute", "Configuration", "Console", "Delegate", "Dictionary", "Directory",
        "Domain", "Endpoint", "Entity", "Enum", "Environment", "Error", "Event", "Exception", "File", "Func",
        "Guid", "Index", "Lazy", "List", "Lock", "Math", "Messages", "Monitor", "Object", "Path", "Persistence",
        "Program", "Random", "Range", "Result", "Results", "Shared", "Stream", "String", "Task", "Thread",
        "Timer", "Tuple", "Type", "Uri", "Version",
    };

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$")]
    private static partial Regex SolutionNamePattern();

    [GeneratedRegex(@"^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex TypeNamePattern();

    [GeneratedRegex(@"(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])")]
    private static partial Regex KebabBoundaryPattern();

    /// <returns>An error message, or <c>null</c> when the name is valid.</returns>
    public static string? ValidateSolutionName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "The solution name cannot be empty."
        : !SolutionNamePattern().IsMatch(name) ? "Use dot-separated identifiers, e.g. Acme.Orders (letters, digits, underscores)."
        : null;

    /// <returns>An error message, or <c>null</c> when the name is valid.</returns>
    public static string? ValidateEntityName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "The entity name cannot be empty."
        : !TypeNamePattern().IsMatch(name) ? "Use a PascalCase singular name, e.g. Product or OrderLine."
        : ReservedEntityNames.Contains(name) ? $"'{name}' clashes with an existing .NET or solution type. Pick a more specific name (e.g. Todo{name})."
        : null;

    /// <returns>An error message, or <c>null</c> when the plural is valid.</returns>
    public static string? ValidatePlural(string? plural, string entityName) =>
        string.IsNullOrWhiteSpace(plural) ? "The plural cannot be empty."
        : !TypeNamePattern().IsMatch(plural) ? "Use a PascalCase name, e.g. Products or People."
        : plural == entityName ? "The plural must differ from the entity name (it's used as the namespace that contains the entity class)."
        : null;

    /// <summary>Simple English pluralization; users can always override it.</summary>
    public static string Pluralize(string name)
    {
        if (name.Length > 1 && name.EndsWith('y') && !"aeiouAEIOU".Contains(name[^2]))
        {
            return string.Concat(name.AsSpan(0, name.Length - 1), "ies");
        }

        if (name.EndsWith('s') || name.EndsWith('x') || name.EndsWith('z')
            || name.EndsWith("ch", StringComparison.Ordinal) || name.EndsWith("sh", StringComparison.Ordinal))
        {
            return name + "es";
        }

        return name + "s";
    }

    /// <summary>OrderLines -> order-lines (matches the template's kebabCase route transform).</summary>
    public static string ToKebabCase(string name) =>
        KebabBoundaryPattern().Replace(name, "-$1$2").ToLowerInvariant();
}
