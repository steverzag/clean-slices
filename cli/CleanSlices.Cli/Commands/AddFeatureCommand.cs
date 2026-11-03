using System.ComponentModel;
using CleanSlices.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CleanSlices.Cli.Commands;

internal sealed class AddFeatureCommand : AsyncCommand<AddFeatureCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[entity]")]
        [Description("Singular PascalCase entity name, e.g. Product. Prompted for when omitted.")]
        public string? Entity { get; init; }

        [CommandOption("-p|--plural <PLURAL>")]
        [Description("Plural name used for folders, namespaces, table and route. Defaults to an English plural of the entity.")]
        public string? Plural { get; init; }

        [CommandOption("--migration")]
        [Description("Create an EF Core migration for the new table (requires dotnet-ef).")]
        public bool Migration { get; init; }

        [CommandOption("--no-migration")]
        [Description("Don't create a migration (default).")]
        public bool NoMigration { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Accept defaults for everything not passed explicitly; never prompt.")]
        public bool Yes { get; init; }

        public override ValidationResult Validate()
        {
            if (Migration && NoMigration)
            {
                return ValidationResult.Error("--migration and --no-migration can't be used together.");
            }

            if (Entity is not null && Naming.ValidateEntityName(Entity) is { } entityError)
            {
                return ValidationResult.Error(entityError);
            }

            if (Entity is not null && Plural is not null && Naming.ValidatePlural(Plural, Entity) is { } pluralError)
            {
                return ValidationResult.Error(pluralError);
            }

            return ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var interactive = Ui.IsInteractive(settings.Yes);
        Ui.Header("add feature");

        if (SolutionContext.Locate(Environment.CurrentDirectory) is not { } solution)
        {
            Ui.Error("No Clean Slices solution found in this directory or its parents.");
            Ui.Hint("Run this inside a solution created with `cleanslices new` (or `dotnet new cleanslices`).");
            return 1;
        }

        AnsiConsole.MarkupLine($"[grey]Solution:[/] {Markup.Escape(solution.AppName)} [grey]({Markup.Escape(solution.Root)})[/]");
        AnsiConsole.WriteLine();

        // Solutions that record their template version render features from that version + their patterns,
        // so e.g. a Result<T> solution gets Result<T>-style features. Others use the installed template.
        var manifest = solution.Manifest;
        var templateSource = manifest.TemplateCommit is null ? null : TemplateRepository.ResolveSource(null, manifest);
        if (templateSource is null && !await Ui.EnsureTemplateInstalledAsync("cs-feature", cancellationToken))
        {
            return 1;
        }

        // ---- Answers -------------------------------------------------------------------------
        var entity = settings.Entity;
        if (entity is null)
        {
            if (!interactive)
            {
                Ui.Error("An entity name is required. Pass it as an argument: cleanslices add feature Product");
                return 1;
            }

            entity = Ui.AskValidated("Entity name [grey](singular, e.g. Product)[/]:", Naming.ValidateEntityName);
        }

        var plural = settings.Plural;
        if (plural is null)
        {
            var suggested = Naming.Pluralize(entity);
            plural = interactive
                ? Ui.AskValidated("Plural:", p => Naming.ValidatePlural(p, entity), defaultValue: suggested)
                : suggested;
        }

        if (Naming.ValidatePlural(plural, entity) is { } pluralError)
        {
            Ui.Error(pluralError);
            return 1;
        }

        if (solution.Manifest.Slices.Any(s => s.Name == entity)
            || File.Exists(Path.Combine(solution.CoreProjectDir, "Domain", plural, $"{entity}.cs")))
        {
            Ui.Error($"A '{entity}' feature already exists in {solution.AppName}.");
            return 1;
        }

        var efAvailable = await Dotnet.IsEfToolAvailableAsync(solution.Root, cancellationToken);
        if (settings.Migration && !efAvailable)
        {
            Ui.Error("--migration was passed but the dotnet-ef tool isn't available.");
            Ui.Hint("Install it with: dotnet tool install --global dotnet-ef");
            return 1;
        }

        var createMigration = efAvailable && !settings.NoMigration
            && (settings.Migration || (interactive && AnsiConsole.Confirm($"Create an EF Core migration [bold]Add{entity}[/] now?", defaultValue: false)));

        AnsiConsole.WriteLine();

        // ---- Generate ------------------------------------------------------------------------
        var slice = new SliceEntry(entity, plural);
        if (templateSource is not null)
        {
            try
            {
                await AnsiConsole.Status().StartAsync($"Adding {entity} feature...", async _ =>
                {
                    using var repository = await TemplateRepository.OpenAsync(templateSource, cancellationToken);
                    var composition = await repository.ComposeAsync(manifest.TemplateCommit!, manifest.Patterns, cancellationToken);
                    await composition.RenderFeatureAsync(solution.AppName, slice, solution.Root, cancellationToken);
                });
            }
            catch (ToolException ex)
            {
                Ui.Failure(ex.Message, ex.Output);
                return 1;
            }
        }
        else
        {
            var generation = await AnsiConsole.Status().StartAsync($"Adding {entity} feature...", _ =>
                Dotnet.RunAsync(
                    solution.Root,
                    ["new", "cs-feature", "-n", entity, "--plural", plural, "--app", solution.AppName],
                    cancellationToken));

            if (!generation.Succeeded)
            {
                Ui.Failure("dotnet new cs-feature failed.", generation.Output);
                return 1;
            }
        }

        Ui.Success($"Added {entity} feature");
        PrintSummary(solution.AppName, entity, plural);

        await manifest.WithSlice(slice).SaveAsync(solution.Root, cancellationToken);
        if (!solution.HasManifestFile)
        {
            Ui.Success($"Created {ProjectManifest.FileName} (this solution didn't have one yet)");
        }

        var migrationName = $"Add{entity}";
        if (createMigration)
        {
            var migration = await AnsiConsole.Status().StartAsync($"Creating migration {migrationName} (builds the solution)...", async _ =>
            {
                // dotnet ef reads project metadata before building, which fails on a never-restored solution.
                var restore = await Dotnet.RunAsync(solution.Root, ["restore", solution.InfrastructureProjectPath], cancellationToken);
                if (!restore.Succeeded)
                {
                    return restore;
                }

                return await Dotnet.RunAsync(
                    solution.Root,
                    ["ef", "migrations", "add", migrationName, "--project", solution.InfrastructureProjectPath],
                    cancellationToken);
            });

            if (migration.Succeeded)
            {
                Ui.Success($"Created migration {migrationName}");
                Ui.NextSteps($"dotnet run --project aspire/{solution.AppName}.AppHost");
                return 0;
            }

            // The feature files are already in place, so this is a partial success.
            Ui.Failure($"Creating migration {migrationName} failed; the feature itself was added.", migration.Output);
            return 1;
        }

        Ui.NextSteps(
            $"dotnet ef migrations add {migrationName} --project {solution.InfrastructureProjectPath}",
            $"dotnet run --project aspire/{solution.AppName}.AppHost");

        return 0;
    }

    private static void PrintSummary(string app, string entity, string plural)
    {
        var route = $"/{Naming.ToKebabCase(plural)}";

        var tree = new Tree("[grey]src[/]");
        tree.AddNode($"{app}.Core/Domain/{plural}/")
            .AddNodes($"{entity}.cs", $"{entity}DomainEvents.cs", $"{entity}Errors.cs");
        var infrastructure = tree.AddNode($"{app}.Infrastructure/");
        infrastructure.AddNode($"Persistence/Configuration/{entity}TypeConfiguration.cs");
        infrastructure.AddNode($"EventHandlers/{plural}/")
            .AddNodes($"{entity}CreatedEventHandler.cs", $"{entity}DeletedEventHandler.cs");
        tree.AddNode($"{app}.Api/Features/{plural}/")
            .AddNodes($"Create{entity}.cs", $"Get{entity}ById.cs", $"Get{plural}.cs", $"Update{entity}Name.cs", $"Delete{entity}.cs", $"{entity}Errors.cs");

        var endpoints = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
            .AddColumn("Method").AddColumn("Route").AddColumn("Endpoint");
        endpoints.AddRow("[green]POST[/]", route, $"Create{entity}");
        endpoints.AddRow("[blue]GET[/]", route, $"Get{plural}");
        endpoints.AddRow("[blue]GET[/]", $"{route}/{{id}}", $"Get{entity}ById");
        endpoints.AddRow("[yellow]PATCH[/]", $"{route}/{{id}}/name", $"Update{entity}Name");
        endpoints.AddRow("[red]DELETE[/]", $"{route}/{{id}}", $"Delete{entity}");

        AnsiConsole.WriteLine();
        AnsiConsole.Write(tree);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(endpoints);
        AnsiConsole.WriteLine();
    }
}
