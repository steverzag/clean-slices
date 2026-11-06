using CleanSlices.Cli.Catalog;
using CleanSlices.Cli.Infrastructure;
using Spectre.Console;

namespace CleanSlices.Cli.Commands;

/// <summary>
/// The selection flow shared by <c>new</c> and <c>add</c>: pick options grouped by concept, answer their choices,
/// resolve, settle unmet requirements, show what will be applied. The resolver decides; this only asks and prints.
/// </summary>
internal static class OptionSelection
{
    private const string NoneLabel = "None";

    /// <summary>A prompt entry: a unit, or none (Spectre's prompts need a non-null item type).</summary>
    private sealed record Pick(Unit? Unit);

    /// <returns>The parsed requests, or null after printing what's wrong.</returns>
    public static List<OptionRequest>? Parse(IEnumerable<string> specs)
    {
        var requests = new List<OptionRequest>();
        foreach (var spec in specs)
        {
            var (request, error) = OptionRequest.Parse(spec);
            if (error is not null)
            {
                Ui.Error(error);
                return null;
            }

            requests.Add(request!);
        }

        return requests;
    }

    /// <summary>Asks which options to add, concept by concept, skipping those already applied.</summary>
    public static List<OptionRequest> Ask(OptionCatalog catalog, bool samples, IReadOnlyCollection<string> applied)
    {
        var picked = new List<string>();
        foreach (var (conceptName, concept) in catalog.Concepts)
        {
            var available = catalog.Units
                .Where(u => u.Kind == UnitKind.Option && u.Concept == conceptName && !applied.Contains(u.Id))
                .ToList();
            if (available.Count == 0)
            {
                continue;
            }

            if (concept.Select == Selection.Many)
            {
                var selected = AnsiConsole.Prompt(new MultiSelectionPrompt<Unit>()
                    .Title($"[bold]{Markup.Escape(concept.Title)}[/]")
                    .NotRequired()
                    .InstructionsText("[grey](space to toggle, enter to accept; none is fine)[/]")
                    .UseConverter(u => Label(catalog, u, samples, [.. applied, .. picked]))
                    .AddChoices(available));
                picked.AddRange(selected.Select(u => u.Id));
                continue;
            }

            foreach (var (groupName, group) in catalog.Groups)
            {
                // Every member, applied or not: the current one must be offered so it can be kept.
                var members = catalog.Units.Where(u => u.Kind == UnitKind.Option && u.Group == groupName && u.Concept == conceptName).ToList();
                if (members.Count == 0 || members.Count == 1 && group.Required)
                {
                    continue;
                }

                // The group's current option (or default) first, so enter keeps it. "None" only for optional groups.
                var current = members.FirstOrDefault(u => applied.Contains(u.Id)) ?? (group.Default is { } d ? catalog.Find(d) : null);
                var choices = new List<Pick>();
                if (current is null)
                {
                    choices.Add(new Pick(null));
                }

                choices.AddRange(members.OrderBy(m => m == current ? 0 : 1).Select(m => new Pick(m)));
                if (current is not null && !group.Required)
                {
                    choices.Add(new Pick(null));
                }

                var choice = AnsiConsole.Prompt(new SelectionPrompt<Pick>()
                    .Title($"[bold]{Markup.Escape(concept.Title)}[/] · {Markup.Escape(group.Title)}")
                    .UseConverter(p => p.Unit is not { } u
                        ? $"{NoneLabel} [grey](the built-in default)[/]"
                        : Label(catalog, u, samples, [.. applied, .. picked]) + (u == current ? " [grey](current)[/]" : string.Empty))
                    .AddChoices(choices));

                // Removing an option isn't supported yet, so "None" over a current option keeps it.
                if (choice.Unit is { } chosen && chosen != current)
                {
                    picked.Add(chosen.Id);
                }
            }
        }

        return [.. picked.Select(id => new OptionRequest(id, new Dictionary<string, string>()))];
    }

    /// <summary>Asks for the choices of the requested options that weren't given; without prompts, defaults apply.</summary>
    public static List<OptionRequest> AskChoices(OptionCatalog catalog, List<OptionRequest> requests, bool interactive)
    {
        if (!interactive)
        {
            return requests;
        }

        return [.. requests.Select(request => AskChoices(catalog, request))];
    }

    private static OptionRequest AskChoices(OptionCatalog catalog, OptionRequest request)
    {
        if (catalog.Find(request.Id) is not { } unit || unit.Choices.Count == 0)
        {
            return request;
        }

        var choices = new Dictionary<string, string>(request.Choices, StringComparer.Ordinal);
        foreach (var (name, choice) in unit.Choices.Where(c => !request.Choices.ContainsKey(c.Key)))
        {
            choices[name] = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"{Markup.Escape(unit.DisplayName)} · [bold]{Markup.Escape(name)}[/]")
                .UseConverter(value => ValueLabel(choice, value))
                .AddChoices(choice.Values.Keys.OrderBy(v => v == choice.Default ? 0 : 1)));
        }

        return request with { Choices = choices };
    }

    /// <summary>
    /// Resolves, and settles what only the user can: interactively, each unmet requirement is answered by adding a
    /// provider or dropping the option that needs it. Prints errors and returns null when it can't be resolved.
    /// </summary>
    public static Resolution? Resolve(
        OptionCatalog catalog,
        IReadOnlyList<OptionRequest> existing,
        List<OptionRequest> requests,
        bool samples,
        bool interactive)
    {
        while (true)
        {
            var resolution = Resolver.Resolve(catalog, existing, requests, samples);
            if (resolution.Errors.Count > 0)
            {
                foreach (var error in resolution.Errors)
                {
                    Ui.Error(error);
                }

                return null;
            }

            if (resolution.Unmet.Count == 0)
            {
                return resolution;
            }

            if (!interactive)
            {
                foreach (var unmet in resolution.Unmet)
                {
                    Ui.Error(unmet.Message);
                }

                Ui.Hint($"Add one with --with, e.g. --with {resolution.Unmet[0].Providers[0].Id}.");
                return null;
            }

            var first = resolution.Unmet[0];
            var droppable = first.RequiredBy.Where(u => requests.Any(r => r.Id == u.Id)).ToList();
            var answer = AnsiConsole.Prompt(new SelectionPrompt<Pick>()
                .Title(Markup.Escape(first.Message))
                .UseConverter(p => p.Unit is not { } u
                    ? $"Drop {Markup.Escape(string.Join(" and ", droppable.Select(d => d.DisplayName)))}"
                    : $"Add [bold]{Markup.Escape(u.DisplayName)}[/]")
                .AddChoices([.. first.Providers.Select(p => new Pick(p)), .. droppable.Count > 0 ? new[] { new Pick(null) } : []]));

            if (answer.Unit is not { } provider)
            {
                requests = [.. requests.Where(r => droppable.All(d => d.Id != r.Id))];
            }
            else
            {
                requests = [.. requests, AskChoices(catalog, new OptionRequest(provider.Id, new Dictionary<string, string>()))];
            }
        }
    }

    /// <summary>What will be applied, why, and the licenses that come with it.</summary>
    public static void PrintPlan(Resolution resolution, IReadOnlyCollection<string> applied)
    {
        var adding = resolution.Units.Where(u => !applied.Contains(u.Unit.Id)).ToList();
        if (adding.Count == 0 && resolution.Replaced.Count == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        foreach (var unit in adding)
        {
            var reason = unit.Origin switch
            {
                Origin.Required => $" [grey](needed by {Markup.Escape(string.Join(", ", unit.AddedBy))})[/]",
                Origin.Glue => $" [grey](joins {Markup.Escape(string.Join(" and ", unit.AddedBy))})[/]",
                Origin.GroupDefault => " [grey](default)[/]",
                _ => string.Empty,
            };
            var choices = unit.Choices.Count == 0
                ? string.Empty
                : $" [grey]{Markup.Escape(string.Join(", ", unit.Choices.Select(c => $"{c.Key}={c.Value}")))}[/]";
            AnsiConsole.MarkupLine($"  [green]+[/] {Markup.Escape(unit.Unit.DisplayName)}{choices}{reason}");
        }

        foreach (var replaced in resolution.Replaced)
        {
            AnsiConsole.MarkupLine($"  [yellow]~[/] replaces {Markup.Escape(replaced.DisplayName)}");
        }

        foreach (var notice in resolution.Licenses)
        {
            Ui.Warning($"{notice.Subject}: {notice.License.Notice}");
            if (notice.License.Setting is { } setting)
            {
                Ui.Hint($"Set {setting} (generated empty) before running the app.");
            }
        }
    }

    /// <summary>Prints the plan and, when prompting, asks to go ahead. True when there's nothing to confirm.</summary>
    public static bool ConfirmPlan(Resolution resolution, IReadOnlyCollection<string> applied, bool interactive)
    {
        PrintPlan(resolution, applied);
        var anything = resolution.Replaced.Count > 0 || resolution.Units.Any(u => !applied.Contains(u.Unit.Id));
        return !interactive || !anything || AnsiConsole.Confirm("Go ahead?", defaultValue: true);
    }

    /// <summary>Titles of the options that were asked for, for messages and commit subjects.</summary>
    public static string Titles(Resolution resolution, Origin origin = Origin.Requested) =>
        string.Join(", ", resolution.Units.Where(u => u.Origin == origin).Select(u => u.Unit.DisplayName));

    private static string Label(OptionCatalog catalog, Unit unit, bool samples, IReadOnlyCollection<string> selected)
    {
        var label = $"[bold]{Markup.Escape(unit.DisplayName)}[/] [grey]{Markup.Escape(unit.Description ?? string.Empty)}[/]";
        var missing = Resolver.MissingFor(catalog, unit, selected);
        if (missing.Count > 0)
        {
            var providers = missing.SelectMany(m => m.Providers).Distinct().Select(p => p.DisplayName);
            label += $" [yellow](needs {Markup.Escape(Resolver.JoinOr(providers))})[/]";
        }

        if (samples && !unit.Samples)
        {
            label += " [yellow](not with the samples)[/]";
        }

        return label;
    }

    private static string ValueLabel(Choice choice, string value)
    {
        var item = choice.Values[value];
        var label = $"[bold]{Markup.Escape(item.Title)}[/]";
        if (item.Description is { } description)
        {
            label += $" [grey]{Markup.Escape(description)}[/]";
        }

        if (value == choice.Default)
        {
            label += " [grey](default)[/]";
        }

        if (item.License is not null)
        {
            label += " [yellow](license)[/]";
        }

        return label;
    }
}
