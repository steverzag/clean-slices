using System.ComponentModel;
using CleanSlices.Cli.Catalog;
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

        [CommandOption("-w|--with <OPTION>")]
        [Description("Include an option (repeatable): <id> or <id>:<choice>=<value>[[,...]], e.g. --with result. See `cleanslices list`.")]
        public string[] With { get; init; } = [];

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
        [Description($"Template repository (local path or git URL). Defaults to ${TemplateRepository.EnvironmentVariable}, then {TemplateRepository.DefaultSource}.")]
        public string? TemplateRepo { get; init; }

        [CommandOption("--template-ref <REF>")]
        [Description($"Branch, tag, commit or HEAD of the template repository to create the solution from. Defaults to ${TemplateRepository.RefEnvironmentVariable}, then {TemplateRepository.DefaultRef}.")]
        public string? TemplateRef { get; init; }

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

    /// <summary>
    /// Opens the template repository. When only the built-in default was used (nothing configured explicitly)
    /// and it can't be reached, e.g. offline, falls back to the installed templates instead of failing.
    /// </summary>
    private static async Task<TemplateRepository?> OpenRepositoryOrFallBackAsync(string source, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            return await AnsiConsole.Status().StartAsync("Fetching templates...", _ => TemplateRepository.OpenAsync(source, cancellationToken));
        }
        catch (ToolException ex) when (TemplateRepository.IsDefaultSource(source) && settings.With.Length == 0)
        {
            Ui.Warning($"Couldn't reach {source}; using the installed templates (no options).");
            Ui.Hint(ex.Output.Trim().Split('\n').LastOrDefault() ?? string.Empty);
            return null;
        }
    }

    private static async Task<int> RunAsync(Settings settings, CancellationToken cancellationToken)
    {
        var interactive = Ui.IsInteractive(settings.Yes);
        Ui.Header("new solution");

        if (OptionSelection.Parse(settings.With) is not { } requested)
        {
            return 1;
        }

        // The solution is rendered from the template repository's main and the commit is recorded,
        // which is what makes `add feature` and `add <option>` render the same template later.
        var source = TemplateRepository.ResolveSource(settings.TemplateRepo, manifest: null);
        var repository = await OpenRepositoryOrFallBackAsync(source, settings, cancellationToken);
        if (repository is null && !await Ui.EnsureTemplateInstalledAsync("cleanslices", cancellationToken))
        {
            return 1;
        }

        using var _ = repository;

        string? templateCommit = null;
        OptionCatalog? catalog = null;
        if (repository is not null)
        {
            templateCommit = await repository.ResolveAndAnnounceAsync(TemplateRepository.ResolveRef(settings.TemplateRef), cancellationToken);
            catalog = await repository.ReadCatalogAsync(templateCommit, cancellationToken);
            if (catalog is null && requested.Count > 0)
            {
                Ui.Error($"The template repository {repository.Source} has no {OptionCatalog.FileName}, so it offers no options.");
                return 1;
            }
        }

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

        Resolution? resolution = null;
        if (catalog is not null)
        {
            if (requested.Count == 0 && interactive)
            {
                requested = OptionSelection.Ask(catalog, samples, applied: []);
            }

            requested = OptionSelection.AskChoices(catalog, requested, interactive);
            resolution = OptionSelection.Resolve(catalog, existing: [], requested, samples, interactive);
            if (resolution is null)
            {
                return 1;
            }

            if (!OptionSelection.ConfirmPlan(resolution, applied: [], interactive))
            {
                Ui.Warning("Nothing was created.");
                return 1;
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
            var composition = await repository.ComposeAsync(templateCommit!, cancellationToken);
            await AnsiConsole.Status().StartAsync("Generating solution...", _ =>
                composition.RenderSolutionAsync(name, samples, resolution?.TemplateArguments ?? [], output, cancellationToken));

            manifest = manifest with
            {
                TemplateRepo = repository.Source,
                TemplateCommit = templateCommit,
                Options = [.. resolution?.Units.Select(OptionEntry.From) ?? []],
            };
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

        if (resolution is not null && OptionSelection.Titles(resolution) is { Length: > 0 } titles)
        {
            extras.Add(titles);
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
