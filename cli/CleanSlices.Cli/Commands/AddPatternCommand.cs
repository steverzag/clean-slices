using System.ComponentModel;
using CleanSlices.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CleanSlices.Cli.Commands;

internal sealed class AddPatternCommand : AsyncCommand<AddPatternCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[pattern]")]
        [Description("Pattern name (a pattern/<name> branch in the template repository). Prompted for when omitted.")]
        public string? Pattern { get; init; }

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
        var interactive = Ui.IsInteractive(settings.Yes);
        Ui.Header("add pattern");

        if (SolutionContext.Locate(Environment.CurrentDirectory) is not { } solution)
        {
            Ui.Error("No Clean Slices solution found in this directory or its parents.");
            return 1;
        }

        var manifest = solution.Manifest;
        AnsiConsole.MarkupLine($"[grey]Solution:[/] {Markup.Escape(solution.AppName)} [grey]({Markup.Escape(solution.Root)})[/]");
        if (manifest.Patterns.Count > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Applied patterns:[/] {Markup.Escape(string.Join(", ", manifest.Patterns))}");
        }

        AnsiConsole.WriteLine();

        if (settings.Pattern is not null && manifest.Patterns.Contains(settings.Pattern))
        {
            Ui.Success($"Pattern '{settings.Pattern}' is already applied.");
            return 0;
        }

        var source = TemplateRepository.ResolveSource(settings.TemplateRepo, manifest);

        if (await PatternMerger.CheckRepositoryAsync(solution.Root, cancellationToken) is { } repositoryError)
        {
            Ui.Error(repositoryError);
            return 1;
        }

        try
        {
            using var repository = await AnsiConsole.Status().StartAsync("Fetching templates...", _ =>
                TemplateRepository.OpenAsync(source, cancellationToken));

            var available = (await repository.ListPatternsAsync(cancellationToken))
                .Where(p => !manifest.Patterns.Contains(p.Name))
                .ToList();

            if (SelectPattern(settings.Pattern, available, manifest, interactive) is not { } pattern)
            {
                return 1;
            }

            var templateCommit = manifest.TemplateCommit;
            if (templateCommit is null)
            {
                // Solutions made before the manifest tracked template versions: assume today's main.
                templateCommit = await repository.GetMainCommitAsync(cancellationToken);
                Ui.Warning($"{ProjectManifest.FileName} doesn't record which template version this solution came from; using main ({templateCommit[..7]}).");
                Ui.Hint("If the solution was generated from an older template, expect extra (unrelated) differences.");
            }

            var outcome = await AnsiConsole.Status().StartAsync($"Applying pattern '{pattern.Name}'...", async status =>
            {
                status.Status("Rendering the solution before and after the pattern...");
                var before = await repository.ComposeAsync(templateCommit, manifest.Patterns, cancellationToken, stableGuids: true);
                var after = await repository.ComposeAsync(templateCommit, [.. manifest.Patterns, pattern.Name], cancellationToken, stableGuids: true);

                var beforeDir = Path.Combine(repository.WorkRoot, "before");
                var afterDir = Path.Combine(repository.WorkRoot, "after");
                await before.RenderProjectAsync(manifest, beforeDir, cancellationToken);
                await after.RenderProjectAsync(manifest, afterDir, cancellationToken);

                status.Status("Merging into your code...");
                return await PatternMerger.MergeAsync(solution.Root, beforeDir, afterDir, cancellationToken);
            });

            var updated = manifest.WithPattern(pattern.Name) with
            {
                TemplateRepo = repository.Source,
                TemplateCommit = templateCommit,
            };
            await updated.SaveAsync(solution.Root, cancellationToken);
            await Git.RunOrThrowAsync(solution.Root, ["add", ProjectManifest.FileName], cancellationToken);

            PrintChanges(outcome);

            if (outcome.HasConflicts)
            {
                Ui.Warning($"Pattern '{pattern.Name}' was merged with conflicts in {outcome.ConflictedFiles.Count} file(s):");
                foreach (var file in outcome.ConflictedFiles)
                {
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(file)}[/]");
                }

                Ui.NextSteps(
                    "Resolve the <<<<<<< / >>>>>>> markers in the files above",
                    "git add -A",
                    $"git commit -m \"feat: apply {pattern.Name} pattern\"",
                    "(or undo everything: git reset --hard HEAD)");
                return 1;
            }

            if (settings.NoCommit)
            {
                Ui.Success($"Applied pattern '{pattern.Name}' (changes are staged, not committed)");
                Ui.NextSteps("git diff --cached", $"git commit -m \"feat: apply {pattern.Name} pattern\"", "dotnet build");
                return 0;
            }

            await Git.RunOrThrowAsync(solution.Root, ["commit", "-q", "-m", $"feat: apply {pattern.Name} pattern\n\n{pattern.Description}\n\nApplied with cleanslices {CliInfo.Version}."], cancellationToken);
            Ui.Success($"Applied pattern '{pattern.Name}' and committed it");
            Ui.NextSteps("dotnet build", "(undo with: git reset --hard HEAD~1)");
            return 0;
        }
        catch (ToolException ex)
        {
            Ui.Failure(ex.Message, ex.Output);
            return 1;
        }
    }

    private static PatternInfo? SelectPattern(string? requested, IReadOnlyList<PatternInfo> available, ProjectManifest manifest, bool interactive)
    {
        if (requested is not null)
        {
            if (manifest.Patterns.Contains(requested))
            {
                Ui.Error($"Pattern '{requested}' is already applied.");
                return null;
            }

            if (available.FirstOrDefault(p => p.Name == requested) is { } match)
            {
                return match;
            }

            Ui.Error($"Unknown pattern '{requested}'.");
            Ui.Hint(available.Count > 0 ? $"Available: {string.Join(", ", available.Select(p => p.Name))}" : "No other patterns are available.");
            return null;
        }

        if (available.Count == 0)
        {
            Ui.Success("Every available pattern is already applied.");
            return null;
        }

        if (!interactive)
        {
            Ui.Error("A pattern name is required. Available: " + string.Join(", ", available.Select(p => p.Name)));
            return null;
        }

        return AnsiConsole.Prompt(new SelectionPrompt<PatternInfo>()
            .Title("Which pattern do you want to add?")
            .UseConverter(p => $"[bold]{Markup.Escape(p.Name)}[/] [grey]{Markup.Escape(p.Description)}[/]")
            .AddChoices(available));
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
