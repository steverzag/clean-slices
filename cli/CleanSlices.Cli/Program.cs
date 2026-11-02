using System.Text;
using CleanSlices.Cli;
using CleanSlices.Cli.Commands;
using Spectre.Console.Cli;

Console.OutputEncoding = Encoding.UTF8;

// Running `cleanslices` with no command starts the interactive "new" flow, like `npm create astro`.
var app = new CommandApp<NewCommand>();

app.Configure(config =>
{
    config.SetApplicationName("cleanslices");
    config.SetApplicationVersion(CliInfo.Version);

    config.AddCommand<NewCommand>("new")
        .WithDescription("Create a new Clean Slices solution.")
        .WithExample("new")
        .WithExample("new", "Acme.Orders", "--samples")
        .WithExample("new", "Acme.Orders", "-y");

    config.AddBranch("add", add =>
    {
        add.SetDescription("Add something to an existing Clean Slices solution.");

        add.AddCommand<AddFeatureCommand>("feature")
            .WithDescription("Add a vertical slice (entity, EF configuration, events, CRUD endpoints).")
            .WithExample("add", "feature")
            .WithExample("add", "feature", "Product")
            .WithExample("add", "feature", "Person", "--plural", "People", "--migration");
    });
});

return await app.RunAsync(args);
