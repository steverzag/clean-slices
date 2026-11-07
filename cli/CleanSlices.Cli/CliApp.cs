using CleanSlices.Cli.Commands;
using Spectre.Console.Cli;

namespace CleanSlices.Cli;

/// <summary>
/// The commands and their help. Spectre reads descriptions as markup, so a literal bracket must be written
/// <c>[[</c> or <c>]]</c>; the help tests render every command to catch one that isn't.
/// </summary>
internal static class CliApp
{
    public static void Configure(IConfigurator config)
    {
        config.SetApplicationName("cleanslices");
        config.SetApplicationVersion(CliInfo.Version);

        config.AddCommand<NewCommand>("new")
            .WithDescription("Create a new Clean Slices solution.")
            .WithExample("new")
            .WithExample("new", "Acme.Orders", "--samples")
            .WithExample("new", "Acme.Orders", "--with", "result", "-y");

        config.AddCommand<ListCommand>("list")
            .WithDescription("List the options the template offers (and, inside a solution, which ones it has).");

        config.AddBranch("add", add =>
        {
            add.SetDescription("Add options (cleanslices add <id>...) or a feature (cleanslices add feature) to an existing solution.");

            add.AddCommand<AddFeatureCommand>("feature")
                .WithDescription("Add a vertical slice (entity, EF configuration, events, CRUD endpoints).")
                .WithExample("add", "feature")
                .WithExample("add", "feature", "Product")
                .WithExample("add", "feature", "Person", "--plural", "People", "--migration");

            // Reached as `add <id>...` (see AddOptionArguments); hidden so help shows that form.
            add.AddCommand<AddOptionCommand>(AddOptionArguments.Command)
                .WithDescription("Merge options into this solution.")
                .IsHidden();
        });
    }
}
