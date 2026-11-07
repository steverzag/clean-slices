using System.Text.Json;
using CleanSlices.Cli.Catalog;
using CleanSlices.Matrix;

// The template repository's CI matrix: which combinations of options to test, derived from catalog.json,
// and how to run one of them. CI calls it; the same commands reproduce a CI job locally.
const string Usage = """
    Usage:
      dotnet run --project cli/CleanSlices.Matrix -- list [--tier push|full] [--json]
      dotnet run --project cli/CleanSlices.Matrix -- run <job> [--ref <template-ref>] [--work <dir>]

    list   the jobs for a tier (push: every push and pull request; full: also pairs and everything; default full).
           --json prints them for GitHub Actions' matrix.
    run    runs one job with the CLI built from source, against this repository at --ref (default HEAD).
    """;

var root = FindRepositoryRoot();
var catalog = OptionCatalog.Parse(File.ReadAllText(Path.Combine(root, OptionCatalog.FileName)));
if (CatalogValidator.Validate(catalog) is { Count: > 0 } errors)
{
    Console.Error.WriteLine($"{OptionCatalog.FileName} is inconsistent:{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errors)}");
    return 2;
}

var plan = MatrixPlanner.Plan(catalog, Combinations.All);

switch (args)
{
    case ["list", .. var rest]:
    {
        var tier = Option(rest, "--tier") is "push" ? Tier.Push : Tier.Full;
        var jobs = plan.Jobs.Where(j => tier == Tier.Full || j.Tier == Tier.Push).ToList();
        if (rest.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(jobs.Select(j => new { id = j.Id, title = $"{j.Id}: {j.Describe()}" })));
            return 0;
        }

        foreach (var job in jobs)
        {
            Console.WriteLine($"{job.Id,-36} {job.Tier,-5} {job.Describe()}{(job.RepositoryVariant ? $"  [+ repository with {string.Join(", ", job.Symbols)}]" : string.Empty)}");
        }

        foreach (var skip in plan.Skipped)
        {
            Console.WriteLine($"{skip.Id,-36} skip  {skip.Reason}");
        }

        return 0;
    }

    case ["run", var id, .. var rest]:
    {
        if (plan.Jobs.FirstOrDefault(j => j.Id == id) is not { } job)
        {
            Console.Error.WriteLine($"Unknown job '{id}'. Run `list` to see them.");
            return 2;
        }

        var work = Option(rest, "--work") ?? Path.Combine(Path.GetTempPath(), "cleanslices-matrix", id);
        var runner = new JobRunner(root, Option(rest, "--ref") ?? "HEAD", Path.GetFullPath(work));
        return await runner.RunAsync(job, CancellationToken.None) ? 0 : 1;
    }

    default:
        Console.Error.WriteLine(Usage);
        return 2;
}

static string? Option(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, OptionCatalog.FileName))
            && Directory.Exists(Path.Combine(directory.FullName, ".template.config")))
        {
            return directory.FullName;
        }
    }

    throw new InvalidOperationException($"Run this inside the template repository (no {OptionCatalog.FileName} found above {Environment.CurrentDirectory}).");
}
