using System.Text;
using CleanSlices.Cli;
using CleanSlices.Cli.Commands;
using Spectre.Console.Cli;

Console.OutputEncoding = Encoding.UTF8;

// Running `cleanslices` with no command starts the interactive "new" flow, like `npm create astro`.
var app = new CommandApp<NewCommand>();
app.Configure(CliApp.Configure);

return await app.RunAsync(AddOptionArguments.Rewrite(args));
