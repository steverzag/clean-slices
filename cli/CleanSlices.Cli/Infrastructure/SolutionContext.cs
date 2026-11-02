namespace CleanSlices.Cli.Infrastructure;

internal sealed record SolutionContext(string Root, ProjectManifest Manifest, bool HasManifestFile)
{
    public string AppName => Manifest.Name;

    public string CoreProjectDir => Path.Combine(Root, "src", $"{AppName}.Core");

    // Forward slashes work on every OS and keep the printed commands copy-pasteable.
    public string InfrastructureProjectPath => $"src/{AppName}.Infrastructure";

    /// <summary>
    /// Finds the enclosing Clean Slices solution: first by its <c>.cleanslices.json</c>,
    /// otherwise by a <c>*.slnx</c> whose name matches the <c>src/&lt;Name&gt;.Api</c> / <c>.Core</c> layout
    /// (solutions created with plain <c>dotnet new</c> before the CLI existed).
    /// </summary>
    public static SolutionContext? Locate(string startDirectory)
    {
        if (ProjectManifest.Find(startDirectory) is { } found)
        {
            return new SolutionContext(found.Root, found.Manifest, HasManifestFile: true);
        }

        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            foreach (var solution in dir.EnumerateFiles("*.slnx"))
            {
                var name = Path.GetFileNameWithoutExtension(solution.Name);
                var src = Path.Combine(dir.FullName, "src");

                if (Directory.Exists(Path.Combine(src, $"{name}.Api")) && Directory.Exists(Path.Combine(src, $"{name}.Core")))
                {
                    var hasSamples = Directory.Exists(Path.Combine(src, $"{name}.Api", "Features", "Tasks"));
                    return new SolutionContext(dir.FullName, ProjectManifest.CreateNew(name, hasSamples), HasManifestFile: false);
                }
            }
        }

        return null;
    }
}
