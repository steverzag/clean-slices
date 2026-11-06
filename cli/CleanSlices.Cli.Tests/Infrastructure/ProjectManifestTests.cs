using CleanSlices.Cli.Infrastructure;

namespace CleanSlices.Cli.Tests.Infrastructure;

public class ProjectManifestTests
{
    [Fact]
    public async Task Version_2_round_trips_with_choices_next_to_the_id()
    {
        var manifest = ProjectManifest.CreateNew("Acme.Orders", samples: true) with
        {
            TemplateCommit = "abc1234",
            Options =
            [
                new OptionEntry("cqrs", new Dictionary<string, string> { ["dispatcher"] = "mediator" }, []),
                new OptionEntry("integration-events", new Dictionary<string, string>(), ["outbox"]),
            ],
            Slices = [new SliceEntry("Product", "Products")],
        };

        var directory = Directory.CreateTempSubdirectory().FullName;
        await manifest.SaveAsync(directory, TestContext.Current.CancellationToken);
        var json = await File.ReadAllTextAsync(Path.Combine(directory, ProjectManifest.FileName), TestContext.Current.CancellationToken);
        Directory.Delete(directory, recursive: true);

        Assert.Contains("\"manifestVersion\": 2", json);
        Assert.Contains("\"id\": \"cqrs\",\n      \"dispatcher\": \"mediator\"", json.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.DoesNotContain("\"addedBy\": []", json);
        Assert.DoesNotContain("optionRequests", json);

        var read = ProjectManifest.Parse(json);
        Assert.True(read.Samples);
        Assert.Equal("mediator", read.Options[0].Choices["dispatcher"]);
        Assert.Equal(["outbox"], read.Options[1].AddedBy);
        Assert.Equal("Products", read.Slices[0].Plural);
        Assert.Equal(["cqrs", "integration-events"], read.OptionRequests.Select(r => r.Id));
    }

    [Fact]
    public void Version_1_without_patterns_is_upgraded()
    {
        var read = ProjectManifest.Parse("""
            {
              "name": "Acme.Orders",
              "cliVersion": "0.2.0",
              "options": { "samples": true },
              "templateRepo": "https://example.com/repo.git",
              "templateCommit": "a63f5be9",
              "patterns": [],
              "slices": [ { "name": "Product", "plural": "Products" } ]
            }
            """);

        Assert.Equal(ProjectManifest.CurrentVersion, read.ManifestVersion);
        Assert.True(read.Samples);
        Assert.Equal("a63f5be9", read.TemplateCommit);
        Assert.Empty(read.Options);
        Assert.Equal("Product", Assert.Single(read.Slices).Name);
    }

    [Fact]
    public void Version_1_with_patterns_points_to_the_old_cli()
    {
        var exception = Assert.Throws<ToolException>(() => ProjectManifest.Parse("""
            { "name": "Acme.Orders", "options": { "samples": false }, "patterns": [ "result" ], "slices": [] }
            """));

        Assert.Contains("'result' from cleanslices 0.2", exception.Message);
        Assert.Contains("use cleanslices 0.2.x", exception.Message);
    }

    [Fact]
    public void A_newer_format_asks_to_update_the_cli()
    {
        var exception = Assert.Throws<ToolException>(() => ProjectManifest.Parse("""{ "manifestVersion": 99, "name": "x" }"""));

        Assert.Contains("Update cleanslices", exception.Message);
    }
}
