using System.ComponentModel;
using CleanSlices.Cli.Catalog;
using CleanSlices.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CleanSlices.Cli.Commands;

/// <summary><c>cleanslices add &lt;option&gt;...</c>: merges options into an existing solution.</summary>
internal sealed class AddOptionCommand : AsyncCommand<AddOptionCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[options]")]
        [Description("Options to add: <id> or <id>:<choice>=<value>[,...]. Several can be added in one merge. Prompted for when omitted.")]
        public string[] Options { get; init; } = [];

        [CommandOption("--template-repo <SOURCE>")]
        [Description($"Template repository (local path or git URL). Defaults to the one recorded in .cleanslices.json, then ${TemplateRepository.EnvironmentVariable}, then {TemplateRepository.DefaultSource}.")]
        public string? TemplateRepo { get; init; }

        [CommandOption("--no-commit")]
        [Description("Leave the merged changes staged instead of committing them.")]
        public bool NoCommit { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Never prompt.")]
        public bool Yes { get; init; }
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
        var interactive = Ui.IsInteractive(settings.Yes);
        Ui.Header("add option");

        if (SolutionContext.Locate(Environment.CurrentDirectory) is not { } solution)
        {
            Ui.Error("No Clean Slices solution found in this directory or its parents.");
            return 1;
        }

        if (OptionSelection.Parse(settings.Options) is not { } requested)
        {
            return 1;
        }

        var manifest = solution.Manifest;
        AnsiConsole.MarkupLine($"[grey]Solution:[/] {Markup.Escape(solution.AppName)} [grey]({Markup.Escape(solution.Root)})[/]");
        var applied = manifest.Options.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        if (applied.Count > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Options:[/] {Markup.Escape(string.Join(", ", manifest.Options.Select(o => o.Id)))}");
        }

        AnsiConsole.WriteLine();

        if (await SolutionMerger.CheckRepositoryAsync(solution.Root, cancellationToken) is { } repositoryError)
        {
            Ui.Error(repositoryError);
            return 1;
        }

        var source = TemplateRepository.ResolveSource(settings.TemplateRepo, manifest);
        using var repository = await AnsiConsole.Status().StartAsync("Fetching templates...", _ =>
            TemplateRepository.OpenAsync(source, cancellationToken));

        var templateCommit = manifest.TemplateCommit;
        if (templateCommit is null)
        {
            // Solutions made before the manifest tracked template versions: assume today's main.
            templateCommit = await repository.GetMainCommitAsync(cancellationToken);
            Ui.Warning($"{ProjectManifest.FileName} doesn't record which template version this solution came from; using main ({templateCommit[..7]}).");
            Ui.Hint("If the solution was generated from an older template, expect extra (unrelated) differences.");
        }

        if (await repository.ReadCatalogAsync(templateCommit, cancellationToken) is not { } catalog)
        {
            Ui.Error($"This solution was created from template {templateCommit[..7]}, before options existed. Options can't be added to it yet.");
            return 1;
        }

        if (requested.Count == 0)
        {
            if (!interactive)
            {
                Ui.Error("Name the options to add, e.g. cleanslices add result. Run `cleanslices list` to see them.");
                return 1;
            }

            requested = OptionSelection.Ask(catalog, manifest.Samples, applied);
            if (requested.Count == 0)
            {
                Ui.Success("Nothing to add.");
                return 0;
            }
        }

        requested = OptionSelection.AskChoices(catalog, [.. requested.Where(r => !applied.Contains(r.Id))], interactive)
            .Concat(requested.Where(r => applied.Contains(r.Id)))
            .ToList();

        var before = OptionSelection.Resolve(catalog, manifest.OptionRequests, [], manifest.Samples, interactive: false);
        var after = OptionSelection.Resolve(catalog, manifest.OptionRequests, requested, manifest.Samples, interactive);
        if (before is null || after is null)
        {
            return 1;
        }

        if (after.Units.All(u => applied.Contains(u.Unit.Id)) && after.Replaced.Count == 0)
        {
            Ui.Success($"Already applied: {string.Join(", ", requested.Select(r => r.Id))}.");
            return 0;
        }

        if (!OptionSelection.ConfirmPlan(after, applied, interactive))
        {
            Ui.Warning("Nothing was changed.");
            return 1;
        }

        var titles = OptionSelection.Titles(after);
        var outcome = await AnsiConsole.Status().StartAsync($"Adding {titles}...", async status =>
        {
            status.Status("Rendering the solution before and after...");
            // One template version; the renders differ only in their option parameters.
            var composition = await repository.ComposeAsync(templateCommit, cancellationToken, stableGuids: true);
            var beforeDir = Path.Combine(repository.WorkRoot, "before");
            var afterDir = Path.Combine(repository.WorkRoot, "after");
            await composition.RenderProjectAsync(manifest, before.TemplateArguments, beforeDir, cancellationToken);
            await composition.RenderProjectAsync(manifest, after.TemplateArguments, afterDir, cancellationToken);

            status.Status("Merging into your code...");
            return await SolutionMerger.MergeAsync(solution.Root, beforeDir, afterDir, cancellationToken);
        });

        var updated = manifest with
        {
            TemplateRepo = repository.Source,
            TemplateCommit = templateCommit,
            Options = [.. after.Units.Select(OptionEntry.From)],
        };
        await updated.SaveAsync(solution.Root, cancellationToken);
        await Git.RunOrThrowAsync(solution.Root, ["add", ProjectManifest.FileName], cancellationToken);

        PrintChanges(outcome);
        WarnAboutReplacements(after);

        var subject = $"feat: add {titles}";
        if (outcome.HasConflicts)
        {
            Ui.Warning($"{titles} merged with conflicts in {outcome.ConflictedFiles.Count} file(s):");
            foreach (var file in outcome.ConflictedFiles)
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape(file)}[/]");
            }

            Ui.NextSteps(
                "Resolve the <<<<<<< / >>>>>>> markers in the files above",
                "git add -A",
                $"git commit -m \"{subject}\"",
                "(or undo everything: git reset --hard HEAD)");
            return 1;
        }

        if (settings.NoCommit)
        {
            Ui.Success($"Added {titles} (changes are staged, not committed)");
            Ui.NextSteps("git diff --cached", $"git commit -m \"{subject}\"", "dotnet build");
            return 0;
        }

        var body = string.Join("\n", after.Units.Where(u => !applied.Contains(u.Unit.Id)).Select(u => $"- {u.Unit.DisplayName}: {u.Unit.Description ?? u.Origin.ToString()}"));
        await Git.RunOrThrowAsync(solution.Root, ["commit", "-q", "-m", $"{subject}\n\n{body}\n\nApplied with cleanslices {CliInfo.Version}."], cancellationToken);
        Ui.Success($"Added {titles} and committed it");
        Ui.NextSteps("dotnet build", "(undo with: git reset --hard HEAD~1)");
        return 0;
    }

    private static void WarnAboutReplacements(Resolution resolution)
    {
        foreach (var replaced in resolution.Replaced)
        {
            var replacement = resolution.Units.First(u => u.Unit.Group == replaced.Group).Unit;
            Ui.Warning($"{replacement.DisplayName} replaced {replaced.DisplayName}.");

            // Migrations are provider-specific (rule 5): the user's existing ones were generated for the old provider.
            if (replaced.Group == "database")
            {
                Ui.Hint("Existing migrations were generated for the previous database: delete them and run `dotnet ef migrations add` again.");
            }
        }
    }

    private static void PrintChanges(MergeOutcome outcome)
    {
        AnsiConsole.WriteLine();
        foreach (var change in outcome.Changes)
        {
            var (color, label) = change.Status switch
            {
                'A' => ("green", "added   "),
                'D' => ("red", "deleted "),
                _ => ("yellow", "modified"),
            };

            var conflict = outcome.ConflictedFiles.Contains(change.Path) ? " [red](conflict)[/]" : string.Empty;
            AnsiConsole.MarkupLine($"  [{color}]{label}[/] {Markup.Escape(change.Path)}{conflict}");
        }

        AnsiConsole.WriteLine();
    }
}
