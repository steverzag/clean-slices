namespace CleanSlices.Cli.Catalog;

/// <summary>Why a unit is in the resolved configuration.</summary>
internal enum Origin
{
    /// <summary>Asked for in this command.</summary>
    Requested,

    /// <summary>Already in the solution.</summary>
    Existing,

    /// <summary>The default of a required group nobody picked.</summary>
    GroupDefault,

    /// <summary>A hidden unit providing what a selected unit requires.</summary>
    Required,

    /// <summary>A glue unit whose units are all selected.</summary>
    Glue,
}

internal sealed record ResolvedUnit(Unit Unit, IReadOnlyDictionary<string, string> Choices, Origin Origin, IReadOnlyList<string> AddedBy);

/// <summary>A capability a selected unit needs that only options (never picked automatically) can provide.</summary>
internal sealed record UnmetRequirement(string Capability, IReadOnlyList<Unit> RequiredBy, IReadOnlyList<Unit> Providers)
{
    public string Message
    {
        get
        {
            var who = string.Join(" and ", RequiredBy.Select(u => u.DisplayName));
            var what = Capability.Replace('-', ' ');
            return $"{who} {(RequiredBy.Count == 1 ? "needs" : "need")} {what}, provided by {Resolver.JoinOr(Providers.Select(p => $"{p.DisplayName} ({p.Id})"))}.";
        }
    }
}

internal sealed record LicenseNotice(string Subject, License License);

/// <param name="Suggestions">
/// Recommended options that aren't selected but now could be: the selection provides what they require and adding
/// them still resolves. The CLI offers them; the resolver never adds them.
/// </param>
internal sealed record Resolution(
    IReadOnlyList<ResolvedUnit> Units,
    IReadOnlyList<Unit> Replaced,
    IReadOnlyList<UnmetRequirement> Unmet,
    IReadOnlyList<string> Errors,
    IReadOnlyList<LicenseNotice> Licenses,
    IReadOnlyList<Unit> Suggestions)
{
    /// <summary>Nothing is wrong and nothing is missing: the configuration can be generated.</summary>
    public bool IsResolved => Errors.Count == 0 && Unmet.Count == 0;

    /// <summary>Template parameters for <c>dotnet new</c>: one bool per unit, one value per choice. Catalog order.</summary>
    public IReadOnlyList<(string Name, string Value)> TemplateArguments =>
    [
        .. Units.SelectMany(r => (IEnumerable<(string, string)>)
        [
            (r.Unit.Symbol, "true"),
            .. r.Choices.Select(c => (r.Unit.ChoiceParameter(c.Key), c.Value)),
        ]),
    ];
}

/// <summary>
/// Turns what the user asked for into a complete, consistent configuration, like a package manager:
/// fills choice defaults, applies group rules, adds hidden units for requirements, rejects conflicts,
/// adds glue units and collects license notices. Applies nothing; the caller renders the result.
/// </summary>
internal static class Resolver
{
    private const int MaxDistanceForSuggestion = 2;

    /// <param name="existing">Options already in the solution (only options are read; hidden and glue units are recomputed).</param>
    public static Resolution Resolve(
        OptionCatalog catalog,
        IReadOnlyList<OptionRequest> existing,
        IReadOnlyList<OptionRequest> requested,
        bool samples) =>
        Resolve(catalog, existing, requested, samples, suggest: true);

    private static Resolution Resolve(
        OptionCatalog catalog,
        IReadOnlyList<OptionRequest> existing,
        IReadOnlyList<OptionRequest> requested,
        bool samples,
        bool suggest)
    {
        var errors = new List<string>();
        var replaced = new List<Unit>();

        // id → (choices, origin). Insertion order doesn't matter: the output is in catalog order.
        var selected = new Dictionary<string, (Dictionary<string, string> Choices, Origin Origin, List<string> AddedBy)>(StringComparer.Ordinal);

        foreach (var request in existing)
        {
            if (catalog.Find(request.Id) is { Kind: UnitKind.Option } unit)
            {
                selected[unit.Id] = (new(request.Choices, StringComparer.Ordinal), Origin.Existing, []);
            }
            else if (catalog.Find(request.Id) is null)
            {
                errors.Add($"The solution uses '{request.Id}', which this template version doesn't know.");
            }
        }

        var requestedUnits = new List<Unit>();
        foreach (var request in requested)
        {
            if (Select(catalog, request, errors) is not { } unit)
            {
                continue;
            }

            if (requestedUnits.Contains(unit))
            {
                errors.Add($"'{unit.Id}' is given more than once.");
                continue;
            }

            requestedUnits.Add(unit);

            if (selected.TryGetValue(unit.Id, out var current))
            {
                var differing = request.Choices.Where(c => !current.Choices.TryGetValue(c.Key, out var v) || v != c.Value).ToList();
                if (differing.Count > 0)
                {
                    errors.Add($"{unit.DisplayName} is already applied with {Describe(current.Choices)}; changing a choice isn't supported yet.");
                }

                continue;
            }

            if (unit.Group is { } group)
            {
                if (requestedUnits.FirstOrDefault(u => u != unit && u.Group == group) is { } other)
                {
                    errors.Add($"{other.DisplayName} and {unit.DisplayName} can't be used together: both are a {GroupTitle(catalog, group)}, and a solution has at most one.");
                    continue;
                }

                // Picking another member of a group replaces the one the solution has.
                foreach (var id in selected.Keys.Where(id => catalog.Find(id)!.Group == group).ToList())
                {
                    replaced.Add(catalog.Find(id)!);
                    selected.Remove(id);
                }
            }

            selected[unit.Id] = (new(request.Choices, StringComparer.Ordinal), Origin.Requested, []);
        }

        // Choices: validate the given ones, fill in defaults.
        foreach (var (id, entry) in selected)
        {
            var unit = catalog.Find(id)!;
            foreach (var (name, value) in entry.Choices.ToList())
            {
                if (!unit.Choices.TryGetValue(name, out var choice))
                {
                    errors.Add(unit.Choices.Count == 0
                        ? $"{unit.DisplayName} has no choices, but '{name}' was given."
                        : $"{unit.DisplayName} has no choice '{name}'. Its choices: {string.Join(", ", unit.Choices.Keys)}.");
                }
                else if (!choice.Values.ContainsKey(value))
                {
                    errors.Add($"'{value}' isn't a value of {unit.DisplayName}'s '{name}'. Values: {string.Join(", ", choice.Values.Keys)}.");
                }
            }

            foreach (var (name, choice) in unit.Choices)
            {
                entry.Choices.TryAdd(name, choice.Default);
            }
        }

        // Required groups always have one option.
        foreach (var (name, group) in catalog.Groups)
        {
            if (group.Required && group.Default is { } defaultId && !selected.Keys.Any(id => catalog.Find(id)!.Group == name)
                && catalog.Find(defaultId) is { } defaultUnit)
            {
                selected[defaultId] = (defaultUnit.Choices.ToDictionary(c => c.Key, c => c.Value.Default, StringComparer.Ordinal), Origin.GroupDefault, []);
            }
        }

        // Requirements, until nothing changes: hidden units can require more units.
        var unmet = new Dictionary<string, (List<Unit> RequiredBy, List<Unit> Providers)>(StringComparer.Ordinal);
        for (var changed = true; changed;)
        {
            changed = false;
            unmet.Clear();
            var provided = selected.Keys.SelectMany(id => catalog.Find(id)!.Capabilities).ToHashSet(StringComparer.Ordinal);

            foreach (var unit in selected.Keys.Select(id => catalog.Find(id)!).ToList())
            {
                foreach (var capability in unit.Requires.Where(c => !provided.Contains(c)))
                {
                    var providers = catalog.Units.Where(u => u != unit && u.Capabilities.Contains(capability)).ToList();
                    if (providers.FirstOrDefault(p => p.Kind == UnitKind.Hidden) is { } hidden)
                    {
                        selected[hidden.Id] = (hidden.Choices.ToDictionary(c => c.Key, c => c.Value.Default, StringComparer.Ordinal), Origin.Required, []);
                        provided.UnionWith(hidden.Capabilities);
                        changed = true;
                    }
                    else if (providers.Any(p => p.Kind == UnitKind.Option))
                    {
                        if (!unmet.TryGetValue(capability, out var missing))
                        {
                            missing = ([], providers.Where(p => p.Kind == UnitKind.Option).ToList());
                            unmet[capability] = missing;
                        }

                        missing.RequiredBy.Add(unit);
                    }
                    else
                    {
                        errors.Add($"{unit.DisplayName} requires '{capability}', which nothing in the catalog provides.");
                    }
                }
            }

        }

        // Who each hidden unit is there for: the selected units that require something it provides.
        foreach (var (id, entry) in selected.Where(s => s.Value.Origin == Origin.Required))
        {
            var hidden = catalog.Find(id)!;
            entry.AddedBy.AddRange(selected.Keys.Where(other => other != id && catalog.Find(other)!.Requires.Any(hidden.Capabilities.Contains)));
        }

        // Conflicts, whichever side declares them, reported once per pair and named in catalog order.
        var units = catalog.Units.Where(u => selected.ContainsKey(u.Id)).ToList();
        for (var i = 0; i < units.Count; i++)
        {
            for (var j = i + 1; j < units.Count; j++)
            {
                var (first, second) = (units[i], units[j]);
                if (first.Conflicts.Any(second.Capabilities.Contains) || second.Conflicts.Any(first.Capabilities.Contains))
                {
                    errors.Add($"{first.DisplayName} and {second.DisplayName} can't be used together.");
                }
            }
        }

        if (samples)
        {
            foreach (var unit in units.Where(u => !u.Samples))
            {
                errors.Add($"{unit.DisplayName} doesn't support the sample features (its documentation says why). Leave out the samples or {unit.DisplayName}.");
            }
        }

        // Glue: every unit it joins is selected.
        foreach (var glue in catalog.Units.Where(u => u.Kind == UnitKind.Glue && u.When.All(selected.ContainsKey)))
        {
            selected[glue.Id] = ([], Origin.Glue, [.. glue.When]);
        }

        var resolved = catalog.Units
            .Where(u => selected.ContainsKey(u.Id))
            .Select(u => new ResolvedUnit(u, selected[u.Id].Choices, selected[u.Id].Origin, selected[u.Id].AddedBy.Distinct().ToList()))
            .ToList();

        var licenses = resolved.SelectMany(LicensesOf).ToList();
        var unmetRequirements = unmet.Select(m => new UnmetRequirement(m.Key, m.Value.RequiredBy.Distinct().ToList(), m.Value.Providers)).ToList();

        // Only for a configuration that resolves: suggestions are about what else fits, not about what's wrong.
        List<Unit> suggestions = [];
        if (suggest && errors.Count == 0 && unmetRequirements.Count == 0)
        {
            var provided = resolved.SelectMany(r => r.Unit.Capabilities).ToHashSet(StringComparer.Ordinal);
            var selection = resolved.Where(r => r.Unit.Kind == UnitKind.Option).Select(r => new OptionRequest(r.Unit.Id, r.Choices)).ToList();
            suggestions =
            [
                .. catalog.Units.Where(candidate =>
                    candidate is { Kind: UnitKind.Option, Recommended: true }
                    && !selected.ContainsKey(candidate.Id)
                    && candidate.Requires.All(provided.Contains)
                    && Resolve(catalog, [], [.. selection, OptionRequest.Of(candidate.Id)], samples, suggest: false).IsResolved),
            ];
        }

        return new Resolution(resolved, replaced, unmetRequirements, errors, licenses, suggestions);
    }

    /// <summary>
    /// What <paramref name="unit"/> would still need if it were added to <paramref name="selectedIds"/>, for annotating
    /// interactive lists ("Inbox (needs outbox or a queue)"). Requirements hidden units satisfy don't count.
    /// </summary>
    public static IReadOnlyList<UnmetRequirement> MissingFor(OptionCatalog catalog, Unit unit, IReadOnlyCollection<string> selectedIds)
    {
        var resolution = Resolve(
            catalog,
            [.. selectedIds.Where(id => id != unit.Id).Select(id => new OptionRequest(id, new Dictionary<string, string>()))],
            [new OptionRequest(unit.Id, new Dictionary<string, string>())],
            samples: false,
            suggest: false);

        return [.. resolution.Unmet.Where(m => m.RequiredBy.Contains(unit))];
    }

    internal static string JoinOr(IEnumerable<string> items)
    {
        var list = items.ToList();
        return list.Count <= 1 ? string.Join(string.Empty, list) : $"{string.Join(", ", list[..^1])} or {list[^1]}";
    }

    private static Unit? Select(OptionCatalog catalog, OptionRequest request, List<string> errors)
    {
        switch (catalog.Find(request.Id))
        {
            case { Kind: UnitKind.Option } unit:
                return unit;
            case { } automatic:
                errors.Add($"'{automatic.Id}' can't be selected: it's added automatically when an option needs it.");
                return null;
            default:
                var suggestion = Suggest(catalog, request.Id);
                errors.Add($"Unknown option '{request.Id}'.{(suggestion is null ? string.Empty : $" Did you mean '{suggestion}'?")} Run `cleanslices list` to see them all.");
                return null;
        }
    }

    private static string? Suggest(OptionCatalog catalog, string id) =>
        catalog.Units
            .Where(u => u.Kind == UnitKind.Option)
            .Select(u => (u.Id, Distance: u.Id.StartsWith(id, StringComparison.Ordinal) && id.Length >= 2 ? 0 : Distance(u.Id, id)))
            .Where(c => c.Distance <= MaxDistanceForSuggestion)
            .OrderBy(c => c.Distance)
            .Select(c => c.Id)
            .FirstOrDefault();

    private static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var previous = row[0];
            row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var current = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), previous + (a[i - 1] == b[j - 1] ? 0 : 1));
                previous = current;
            }
        }

        return row[b.Length];
    }

    private static string GroupTitle(OptionCatalog catalog, string group) =>
        catalog.Groups.TryGetValue(group, out var g) ? g.Title.ToLowerInvariant() : group;

    private static string Describe(IReadOnlyDictionary<string, string> choices) =>
        choices.Count == 0 ? "no choices" : string.Join(", ", choices.Select(c => $"{c.Key}={c.Value}"));

    private static IEnumerable<LicenseNotice> LicensesOf(ResolvedUnit resolved)
    {
        if (resolved.Unit.License is { } license)
        {
            yield return new LicenseNotice(resolved.Unit.DisplayName, license);
        }

        foreach (var (name, value) in resolved.Choices)
        {
            if (resolved.Unit.Choices.TryGetValue(name, out var choice)
                && choice.Values.TryGetValue(value, out var choiceValue)
                && choiceValue.License is { } valueLicense)
            {
                yield return new LicenseNotice($"{resolved.Unit.DisplayName} ({choiceValue.Title})", valueLicense);
            }
        }
    }
}
