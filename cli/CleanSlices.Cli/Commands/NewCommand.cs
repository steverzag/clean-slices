using System.ComponentModel;
using CleanSlices.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CleanSlices.Cli.Commands;

internal sealed class NewCommand : AsyncCommand<NewCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[name]")]
        [Description("Solution name, e.g. Acme.Orders. Prompted for when omitted.")]
        public string? Name { get; init; }

        [CommandOption("-o|--output <DIR>")]
        [Description("Output directory. Defaults to ./<name>.")]
        public string? Output { get; init; }

        [CommandOption("--samples")]
        [Description("Include the sample Tasks/Users features.")]
        public bool Samples { get; init; }

        [CommandOption("--no-samples")]
        [Description("Don't include the sample features (default).")]
        public bool NoSamples { get; init; }

        [CommandOption("--git")]
        [Description("Initialize a git repository with an initial commit (default when git is available).")]
        public bool Git { get; init; }

        [CommandOption("--no-git")]
        [Description("Skip git initialization.")]
        public bool NoGit { get; init; }

        [CommandOption("--no-restore")]
        [Description("Skip restoring NuGet packages after generation.")]
        public bool NoRestore { get; init; }

        [CommandOption("--template-repo <SOURCE>")]
        [Description($"Template repository (local path or git URL). Enables patterns and later `add pattern`. Defaults to ${TemplateRepository.EnvironmentVariable}.")]
        public string? TemplateRepo { get; init; }

        [CommandOption("-p|--pattern <NAME>")]
        [Description("Include a pattern (repeatable), e.g. --pattern result. Prompted for when a template repository is available.")]
        public string[] Patterns { get; init; } = [];

        [CommandOption("-y|--yes")]
        [Description("Accept defaults for everything not passed explicitly; never prompt.")]
        public bool Yes { get; init; }

        public override ValidationResult Validate()
        {
            if (Samples && NoSamples)
            {
                return ValidationResult.Error("--samples and --no-samples can't be used together.");
            }

            if (Git && NoGit)
            {
                return ValidationResult.Error("--git and --no-git can't be used together.");
            }

            if (Name is not null && Naming.ValidateSolutionName(Name) is { } error)
            {
                return ValidationResult.Error(error);
            }

            return ValidationResult.Success();
        }
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
        Ui.Header("new solution");

        // With a template repository, the solution is rendered from main (+ patterns) and the commit is recorded,
        // which is what makes `add pattern` possible later. Without one, the installed templates are used.
        var source = TemplateRepository.ResolveSource(settings.TemplateRepo, manifest: null);
        if (source is null)
        {
            if (settings.Patterns.Length > 0)
            {
                Ui.Error($"--pattern needs a template repository: pass --template-repo <path-or-url> or set {TemplateRepository.EnvironmentVariable}.");
                return 1;
            }

            if (!await Ui.EnsureTemplateInstalledAsync("cleanslices", cancellationToken))
            {
                return 1;
            }
        }

        using var repository = source is null
            ? null
            : await AnsiConsole.Status().StartAsync("Fetching templates...", _ => TemplateRepository.OpenAsync(source, cancellationToken));

        // ---- Answers -------------------------------------------------------------------------
        var name = settings.Name;
        if (name is null)
        {
            if (!interactive)
            {
                Ui.Error("A solution name is required. Pass it as an argument: cleanslices new Acme.Orders");
                return 1;
            }

            name = Ui.AskValidated("Solution name [grey](e.g. Acme.Orders)[/]:", Naming.ValidateSolutionName);
        }

        var output = Path.GetFullPath(settings.Output ?? name);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        {
            Ui.Error($"The directory '{output}' already exists and is not empty.");
            return 1;
        }

        var samples = settings.Samples
            || (!settings.NoSamples && interactive
                && AnsiConsole.Confirm("Include the sample [bold]Tasks/Users[/] features?", defaultValue: false));

        IReadOnlyList<string> patterns = [];
        if (repository is not null)
        {
            var available = await repository.ListPatternsAsync(cancellationToken);
            if (settings.Patterns.Length > 0)
            {
                var unknown = settings.Patterns.Where(p => available.All(a => a.Name != p)).ToList();
                if (unknown.Count > 0)
                {
                    Ui.Error($"Unknown pattern(s): {string.Join(", ", unknown)}.");
                    Ui.Hint(available.Count > 0 ? $"Available: {string.Join(", ", available.Select(a => a.Name))}" : "The template repository has no patterns.");
                    return 1;
                }

                patterns = settings.Patterns.Distinct().ToList();
            }
            else if (interactive && available.Count > 0)
            {
                patterns = AnsiConsole.Prompt(new MultiSelectionPrompt<PatternInfo>()
                        .Title("Which [bold]patterns[/] do you want to include?")
                        .NotRequired()
                        .InstructionsText("[grey](space to toggle, enter to accept; none is fine)[/]")
                        .UseConverter(p => $"[bold]{Markup.Escape(p.Name)}[/] [grey]{Markup.Escape(p.Description)}[/]")
                        .AddChoices(available))
                    .Select(p => p.Name)
                    .ToList();
            }
        }

        var gitAvailable = await Git.IsAvailableAsync(cancellationToken);
        if (settings.Git && !gitAvailable)
        {
            Ui.Error("--git was passed but git is not installed or not on PATH.");
            return 1;
        }

        var initGit = gitAvailable && !settings.NoGit
            && (settings.Git || !interactive || AnsiConsole.Confirm("Initialize a [bold]git[/] repository?", defaultValue: true));

        AnsiConsole.WriteLine();

        // ---- Generate ------------------------------------------------------------------------
        var manifest = ProjectManifest.CreateNew(name, samples);

        if (repository is not null)
        {
            var templateCommit = await repository.GetMainCommitAsync(cancellationToken);
            await AnsiConsole.Status().StartAsync("Generating solution...", async _ =>
            {
                var composition = await repository.ComposeAsync(templateCommit, patterns, cancellationToken);
                await composition.RenderSolutionAsync(name, samples, output, cancellationToken);
            });

            manifest = manifest with { TemplateRepo = repository.Source, TemplateCommit = templateCommit, Patterns = patterns };
        }
        else
        {
            var generation = await AnsiConsole.Status().StartAsync("Generating solution...", _ =>
            {
                List<string> args = ["new", "cleanslices", "-n", name, "-o", output];
                if (samples)
                {
                    args.Add("--samples");
                }

                return Dotnet.RunAsync(Environment.CurrentDirectory, args, cancellationToken);
            });

            if (!generation.Succeeded)
            {
                Ui.Failure("dotnet new cleanslices failed.", generation.Output);
                return 1;
            }
        }

        var extras = new List<string>();
        if (samples)
        {
            extras.Add("sample features");
        }

        if (patterns.Count > 0)
        {
            extras.Add($"pattern(s): {string.Join(", ", patterns)}");
        }

        Ui.Success($"Created {name}{(extras.Count > 0 ? $" with {string.Join("; ", extras)}" : string.Empty)}");

        await manifest.SaveAsync(output, cancellationToken);
        Ui.Success($"Wrote {ProjectManifest.FileName}");

        if (initGit)
        {
            var git = await AnsiConsole.Status().StartAsync("Initializing git repository...", _ =>
                Git.InitAndCommitAsync(output, $"chore: scaffold {name} with cleanslices {CliInfo.Version}", cancellationToken));

            if (git.Succeeded)
            {
                Ui.Success("Initialized git repository with an initial commit");
            }
            else
            {
                // Typically a missing user.name/user.email; the repo exists, only the commit is missing.
                Ui.Warning("Git setup didn't fully complete; you can finish it manually.");
                Ui.Hint(git.Output.Trim());
            }
        }

        // After the git commit, so the initial commit is the pristine template output.
        if (!settings.NoRestore)
        {
            var restore = await AnsiConsole.Status().StartAsync("Restoring packages...", _ =>
                Dotnet.RunAsync(output, ["restore", $"{name}.slnx"], cancellationToken));

            if (restore.Succeeded)
            {
                Ui.Success("Restored packages");
            }
            else
            {
                Ui.Warning("Package restore failed; run `dotnet restore` once the issue is fixed.");
                Ui.Hint(restore.Output.Trim());
            }
        }

        var relativeOutput = Path.GetRelativePath(Environment.CurrentDirectory, output);
        Ui.NextSteps(
            $"cd {relativeOutput}",
            "cleanslices add feature",
            $"dotnet run --project aspire/{name}.AppHost");

        return 0;
    }
}
