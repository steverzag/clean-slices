using CleanSlices.Cli.Catalog;

namespace CleanSlices.Cli.Tests.Catalog;

public class CatalogTests
{
    [Fact]
    public void A_newer_format_asks_to_update_the_cli()
    {
        var exception = Assert.Throws<CatalogException>(() =>
            OptionCatalog.Parse($$"""{ "catalogVersion": {{OptionCatalog.SupportedVersion + 1}}, "whatever": [] }"""));

        Assert.Contains("Update cleanslices", exception.Message);
    }

    [Theory]
    [InlineData("""{ "units": [] }""", "no valid catalogVersion")]
    [InlineData("""{ "catalogVersion": 1, """, "not valid JSON")]
    [InlineData("""{ "catalogVersion": 1, "units": [{ "id": "x", "kind": "plugin" }] }""", "doesn't match the catalog format")]
    public void Invalid_files_are_rejected_with_a_reason(string json, string expected)
    {
        Assert.Contains(expected, Assert.Throws<CatalogException>(() => OptionCatalog.Parse(json)).Message);
    }

    [Fact]
    public void Order_is_kept_for_concepts_groups_and_choice_values()
    {
        var catalog = FakeCatalog.World;

        Assert.Equal(["pattern", "infrastructure", "strategy"], catalog.Concepts.Keys);
        Assert.Equal(["database", "queue", "cache", "realtime"], catalog.Groups.Keys);
        Assert.Equal(["handlers", "mediator", "mediatr"], catalog.Find("cqrs")!.Choices["dispatcher"].Values.Keys);
    }

    [Fact]
    public void Kinds_selections_and_defaults_are_read()
    {
        var catalog = FakeCatalog.World;

        Assert.Equal(Selection.OnePerGroup, catalog.Concepts["infrastructure"].Select);
        Assert.Equal(UnitKind.Hidden, catalog.Find("integration-events")!.Kind);
        Assert.Equal(UnitKind.Glue, catalog.Find("signalr-redis")!.Kind);
        Assert.True(catalog.Find("result")!.Samples);
        Assert.False(catalog.Find("legacy")!.Samples);
        Assert.Equal("integration_events", catalog.Find("integration-events")!.Symbol);
        Assert.True(catalog.Find("inbox")!.Recommended);
        Assert.True(catalog.Find("outbox")!.AddsTables);
        Assert.False(catalog.Find("result")!.Recommended || catalog.Find("result")!.AddsTables);
        Assert.Equal("convert existing entities by hand", catalog.Find("outbox")!.AddNote);
        Assert.Null(catalog.Find("result")!.AddNote);
    }
}
