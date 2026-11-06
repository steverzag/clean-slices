namespace CleanSlices.Cli.Catalog;

/// <summary>
/// The rules <c>catalog.schema.json</c> can't express because they cross-reference parts of the catalog.
/// Run by the consistency test before a release and by the CLI when it loads a catalog.
/// </summary>
internal static class CatalogValidator
{
    public static IReadOnlyList<string> Validate(OptionCatalog catalog)
    {
        var errors = new List<string>();

        foreach (var duplicate in catalog.Units.GroupBy(u => u.Id).Where(g => g.Count() > 1))
        {
            errors.Add($"Unit '{duplicate.Key}' is declared {duplicate.Count()} times.");
        }

        foreach (var unit in catalog.Units)
        {
            ValidateUnit(catalog, unit, errors);
        }

        foreach (var (name, group) in catalog.Groups)
        {
            var members = catalog.Units.Where(u => u.Group == name).ToList();
            if (members.Count == 0)
            {
                errors.Add($"Group '{name}' has no options.");
            }

            if (group.Default is { } defaultId && members.All(m => m.Id != defaultId))
            {
                errors.Add($"Group '{name}' has default '{defaultId}', which isn't an option of the group.");
            }
        }

        // Every unit, choice parameter and choice value becomes a template symbol, so they all share one namespace.
        // Duplicate ids are reported above, so each id counts once here.
        var symbols = catalog.Units.DistinctBy(u => u.Id).SelectMany(u => SymbolsOf(u).Select(symbol => (Symbol: symbol, Owner: u.Id))).ToList();
        foreach (var clash in symbols.GroupBy(s => s.Symbol).Where(g => g.Count() > 1))
        {
            errors.Add($"Template symbol '{clash.Key}' is used more than once ({string.Join(", ", clash.Select(c => c.Owner))}).");
        }

        foreach (var reserved in symbols.Where(s => ReservedSymbols.Contains(s.Symbol)))
        {
            errors.Add($"Template symbol '{reserved.Symbol}' (from '{reserved.Owner}') is reserved by the template.");
        }

        return errors;
    }

    /// <summary>Template parameters that aren't options.</summary>
    public static readonly IReadOnlySet<string> ReservedSymbols = new HashSet<string>(StringComparer.Ordinal) { "samples", "name", "app", "plural" };

    /// <summary>The template symbols a unit introduces: its own, one parameter per choice, and one bool per choice value.</summary>
    public static IEnumerable<string> SymbolsOf(Unit unit)
    {
        yield return unit.Symbol;
        foreach (var (name, choice) in unit.Choices)
        {
            yield return unit.ChoiceParameter(name);
            foreach (var value in choice.Values.Keys)
            {
                yield return value;
            }
        }
    }

    private static void ValidateUnit(OptionCatalog catalog, Unit unit, List<string> errors)
    {
        var where = $"Unit '{unit.Id}'";

        if (unit.Concept is { } conceptName)
        {
            if (!catalog.Concepts.TryGetValue(conceptName, out var concept))
            {
                errors.Add($"{where} has unknown concept '{conceptName}'.");
            }
            else if (concept.Select == Selection.OnePerGroup && unit.Group is null)
            {
                errors.Add($"{where} belongs to '{conceptName}', which selects one option per group, but has no group.");
            }
            else if (concept.Select == Selection.Many && unit.Group is not null)
            {
                errors.Add($"{where} has a group, but its concept '{conceptName}' doesn't use groups.");
            }
        }

        if (unit.Group is { } group && !catalog.Groups.ContainsKey(group))
        {
            errors.Add($"{where} has unknown group '{group}'.");
        }

        foreach (var (name, choice) in unit.Choices)
        {
            if (!choice.Values.ContainsKey(choice.Default))
            {
                errors.Add($"{where}, choice '{name}': the default '{choice.Default}' isn't one of its values.");
            }
        }

        foreach (var capability in unit.Requires)
        {
            if (!catalog.Units.Any(u => u != unit && u.Capabilities.Contains(capability)))
            {
                errors.Add($"{where} requires '{capability}', which no unit provides.");
            }
        }

        foreach (var conflict in unit.Conflicts)
        {
            if (!catalog.Units.Any(u => u != unit && u.Capabilities.Contains(conflict)))
            {
                errors.Add($"{where} conflicts with '{conflict}', which no unit provides.");
            }
        }

        foreach (var id in unit.When)
        {
            if (catalog.Find(id) is null)
            {
                errors.Add($"{where} applies when '{id}' is selected, but there's no such unit.");
            }
        }
    }
}
