using System.ComponentModel;
using CleanSlices.Cli.Catalog;
using CleanSlices.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CleanSlices.Cli.Commands;

/// <summary><c>cleanslices list</c>: the options the template offers, and inside a solution, which ones it has.</summary>
internal sealed class ListCommand : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--template-repo <SOURCE>")]
        [Description($"Template repository (local path or git URL). Defaults to the solution's, then ${TemplateRepository.EnvironmentVariable}, then {TemplateRepository.DefaultSource}.")]
        public string? TemplateRepo { get; init; }

        [CommandOption("--template-ref <REF>")]
        [Description($"Outside a solution: branch, tag, commit or HEAD of the template repository to list. Defaults to ${TemplateRepository.RefEnvironmentVariable}, then {TemplateRepository.DefaultRef}.")]
        public string? TemplateRef { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            return await RunAsync(settings, cancellationToken);
        }
        catch (ToolException ex)
        {
            Ui.Failure(ex.Message, ex.Output);
            return 1;
        }
    }

    private static async Task<int> RunAsync(Settings settings, CancellationToken cancellationToken)
    {
        Ui.Header("options");

        // Inside a solution, list what its template version offers; elsewhere, what main offers.
        var solution = SolutionContext.Locate(Environment.CurrentDirectory);
        var manifest = solution?.HasManifestFile == true ? solution.Manifest : null;
        var source = TemplateRepository.ResolveSource(settings.TemplateRepo, manifest);

        using var repository = await AnsiConsole.Status().StartAsync("Fetching templates...", _ =>
            TemplateRepository.OpenAsync(source, cancellationToken));
        var commit = manifest?.TemplateCommit
            ?? await repository.ResolveAndAnnounceAsync(TemplateRepository.ResolveRef(settings.TemplateRef), cancellationToken);

        if (await repository.ReadCatalogAsync(commit, cancellationToken) is not { } catalog)
        {
            Ui.Error(manifest is null
                ? $"The template repository {repository.Source} offers no options."
                : $"This solution was created from template {commit[..7]}, before options existed.");
            return 1;
        }

        var applied = manifest?.Options.ToDictionary(o => o.Id, StringComparer.Ordinal) ?? [];
        if (manifest is not null)
        {
            AnsiConsole.MarkupLine($"[grey]Solution:[/] {Markup.Escape(manifest.Name)} [grey](template {commit[..7]})[/]");
            AnsiConsole.WriteLine();
        }

        foreach (var (conceptName, concept) in catalog.Concepts)
        {
            var units = catalog.Units.Where(u => u.Kind == UnitKind.Option && u.Concept == conceptName).ToList();
            if (units.Count == 0)
            {
                continue;
            }

            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(concept.Title)}[/]");
            var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
                .AddColumn(string.Empty).AddColumn("Id").AddColumn("Option").AddColumn("Details");

            foreach (var unit in units.OrderBy(u => u.Group is null ? -1 : catalog.Groups.IndexOf(u.Group)))
            {
                var details = new List<string>();
                if (unit.Group is { } group && catalog.Groups.TryGetValue(group, out var g))
                {
                    details.Add(Markup.Escape($"{g.Title}{(g.Default == unit.Id ? " (default)" : string.Empty)}"));
                }

                if (unit.Description is { } description)
                {
                    details.Add(Markup.Escape(description));
                }

                foreach (var (name, choice) in unit.Choices)
                {
                    var current = applied.TryGetValue(unit.Id, out var entry) && entry.Choices.TryGetValue(name, out var v) ? v : null;
                    var values = choice.Values.Keys.Select(value => value == (current ?? choice.Default) ? $"[bold]{Markup.Escape(value)}[/]" : Markup.Escape(value));
                    details.Add($"{Markup.Escape(name)}: {string.Join(" | ", values)}");
                }

                var needs = Resolver.MissingFor(catalog, unit, []).SelectMany(m => m.Providers).Distinct().ToList();
                if (needs.Count > 0)
                {
                    details.Add($"[yellow]needs {Markup.Escape(Resolver.JoinOr(needs.Select(n => n.Id)))}[/]");
                }

                if (!unit.Samples)
                {
                    details.Add("[yellow]not with the samples[/]");
                }

                if (unit.License is not null || unit.Choices.Values.Any(c => c.Values.Values.Any(v => v.License is not null)))
                {
                    details.Add("[yellow]license notice[/]");
                }

                table.AddRow(
                    applied.ContainsKey(unit.Id) ? "[green]✔[/]" : string.Empty,
                    $"[aqua]{Markup.Escape(unit.Id)}[/]",
                    Markup.Escape(unit.DisplayName),
                    string.Join("\n", details));
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        var automatic = applied.Values.Where(o => catalog.Find(o.Id)?.Kind is UnitKind.Hidden or UnitKind.Glue).ToList();
        if (automatic.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Added automatically[/]");
            foreach (var entry in automatic)
            {
                AnsiConsole.MarkupLine($"  [green]✔[/] [aqua]{Markup.Escape(entry.Id)}[/] [grey](for {Markup.Escape(string.Join(", ", entry.AddedBy))})[/]");
            }

            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine(manifest is null
            ? "[grey]Use them with[/] [aqua]cleanslices new <name> --with <id>[/]"
            : "[grey]Add one with[/] [aqua]cleanslices add <id>[/]");
        return 0;
    }
}
