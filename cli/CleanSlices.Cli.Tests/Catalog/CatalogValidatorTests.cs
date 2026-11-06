using CleanSlices.Cli.Catalog;

namespace CleanSlices.Cli.Tests.Catalog;

public class CatalogValidatorTests
{
    private const string Option = """ "kind": "option", "title": "T", "description": "d" """;

    private static string Error(string units, string groups = "{}") =>
        Assert.Single(CatalogValidator.Validate(FakeCatalog.Build(units, groups)));

    [Fact]
    public void The_fake_world_is_valid()
    {
        Assert.Empty(CatalogValidator.Validate(FakeCatalog.World));
    }

    [Fact]
    public void Duplicate_ids_are_reported()
    {
        Assert.Contains("declared 2 times", Error($$"""[{ "id": "a", {{Option}}, "concept": "pattern" }, { "id": "a", {{Option}}, "concept": "pattern" }]"""));
    }

    [Fact]
    public void Unknown_concepts_are_reported()
    {
        Assert.Contains("unknown concept 'tool'", Error($$"""[{ "id": "a", {{Option}}, "concept": "tool" }]"""));
    }

    [Fact]
    public void Options_of_a_one_per_group_concept_need_a_group()
    {
        Assert.Contains("has no group", Error($$"""[{ "id": "a", {{Option}}, "concept": "infrastructure" }]"""));
    }

    [Fact]
    public void Options_of_other_concepts_cant_have_a_group()
    {
        Assert.Contains("doesn't use groups", Error(
            $$"""[{ "id": "a", {{Option}}, "concept": "pattern", "group": "g" }]""",
            """{ "g": { "title": "G", "required": false } }"""));
    }

    [Fact]
    public void Unknown_groups_are_reported()
    {
        Assert.Contains("unknown group 'queue'", Error($$"""[{ "id": "a", {{Option}}, "concept": "infrastructure", "group": "queue" }]"""));
    }

    [Fact]
    public void Groups_without_options_are_reported()
    {
        Assert.Contains("has no options", Error("[]", """{ "queue": { "title": "Queue", "required": false } }"""));
    }

    [Fact]
    public void A_group_default_must_be_one_of_its_options()
    {
        Assert.Contains("isn't an option of the group", Error(
            $$"""[{ "id": "a", {{Option}}, "concept": "infrastructure", "group": "database" }]""",
            """{ "database": { "title": "Database", "required": true, "default": "b" } }"""));
    }

    [Fact]
    public void A_choice_default_must_be_one_of_its_values()
    {
        Assert.Contains("isn't one of its values", Error($$"""
            [{ "id": "a", {{Option}}, "concept": "pattern",
               "choices": { "c": { "default": "z", "values": { "x": { "title": "X" }, "y": { "title": "Y" } } } } }]
            """));
    }

    [Fact]
    public void Requirements_need_a_provider()
    {
        Assert.Contains("which no unit provides", Error($$"""[{ "id": "a", {{Option}}, "concept": "pattern", "requires": ["b"] }]"""));
    }

    [Fact]
    public void A_unit_doesnt_satisfy_its_own_requirement()
    {
        Assert.Contains("which no unit provides", Error($$"""[{ "id": "a", {{Option}}, "concept": "pattern", "provides": ["b"], "requires": ["b"] }]"""));
    }

    [Fact]
    public void Conflicts_must_name_something_that_exists()
    {
        Assert.Contains("conflicts with 'b'", Error($$"""[{ "id": "a", {{Option}}, "concept": "pattern", "conflicts": ["b"] }]"""));
    }

    [Fact]
    public void Glue_units_must_name_existing_units()
    {
        Assert.Contains("no such unit", Error($$"""[{ "id": "a", {{Option}}, "concept": "pattern" }, { "id": "g", "kind": "glue", "when": ["a", "b"] }]"""));
    }

    [Fact]
    public void Choice_values_share_the_symbol_namespace_with_unit_ids()
    {
        Assert.Contains("'mediator' is used more than once", Error($$"""
            [{ "id": "mediator", {{Option}}, "concept": "pattern" },
             { "id": "cqrs", {{Option}}, "concept": "pattern",
               "choices": { "dispatcher": { "default": "handlers", "values": { "handlers": { "title": "H" }, "mediator": { "title": "M" } } } } }]
            """));
    }

    [Fact]
    public void An_id_and_a_choice_parameter_can_clash()
    {
        // "a-b" becomes the symbol a_b; choice "b" of "a" becomes the parameter a_b too.
        Assert.Contains("'a_b' is used more than once", Error($$"""
            [{ "id": "a-b", {{Option}}, "concept": "pattern" },
             { "id": "a", {{Option}}, "concept": "pattern",
               "choices": { "b": { "default": "x", "values": { "x": { "title": "X" }, "y": { "title": "Y" } } } } }]
            """));
    }

    [Fact]
    public void Template_parameters_are_reserved()
    {
        Assert.Contains("reserved by the template", Error($$"""[{ "id": "samples", {{Option}}, "concept": "pattern" }]"""));
    }
}
