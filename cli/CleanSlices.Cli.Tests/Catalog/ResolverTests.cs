using CleanSlices.Cli.Catalog;

namespace CleanSlices.Cli.Tests.Catalog;

public class ResolverTests
{
    private static Resolution Resolve(params string[] requested) => Resolve([], requested);

    private static Resolution Resolve(string[] existing, string[] requested, bool samples = false) =>
        Resolver.Resolve(FakeCatalog.World, [.. existing.Select(Parse)], [.. requested.Select(Parse)], samples);

    private static OptionRequest Parse(string spec) => OptionRequest.Parse(spec).Request!;

    private static IEnumerable<string> Ids(Resolution resolution) => resolution.Units.Select(u => u.Unit.Id);

    private static ResolvedUnit Unit(Resolution resolution, string id) => resolution.Units.Single(u => u.Unit.Id == id);

    [Fact]
    public void Nothing_selected_gives_the_default_of_each_required_group()
    {
        var resolution = Resolve();

        Assert.True(resolution.IsResolved);
        Assert.Equal(["sqlserver"], Ids(resolution));
        Assert.Equal(Origin.GroupDefault, Unit(resolution, "sqlserver").Origin);
    }

    [Fact]
    public void A_selected_option_becomes_a_template_argument()
    {
        var resolution = Resolve("result");

        Assert.True(resolution.IsResolved);
        Assert.Equal(Origin.Requested, Unit(resolution, "result").Origin);
        Assert.Equal([("result", "true"), ("sqlserver", "true")], resolution.TemplateArguments);
    }

    [Fact]
    public void Units_come_out_in_catalog_order()
    {
        var resolution = Resolve("seq", "audit", "result");

        Assert.Equal(["result", "audit", "sqlserver", "seq", "current-user"], Ids(resolution));
    }

    [Fact]
    public void An_unknown_option_suggests_the_closest_one()
    {
        var resolution = Resolve("resutl");

        Assert.False(resolution.IsResolved);
        Assert.Contains("Did you mean 'result'?", Assert.Single(resolution.Errors));
    }

    [Fact]
    public void An_unknown_option_far_from_any_has_no_suggestion()
    {
        var error = Assert.Single(Resolve("kubernetes").Errors);

        Assert.StartsWith("Unknown option 'kubernetes'.", error);
        Assert.DoesNotContain("Did you mean", error);
    }

    [Theory]
    [InlineData("integration-events")]
    [InlineData("signalr-redis")]
    public void Hidden_and_glue_units_cant_be_selected(string id)
    {
        Assert.Contains("added automatically", Assert.Single(Resolve(id).Errors));
    }

    [Fact]
    public void The_same_option_twice_is_rejected()
    {
        Assert.Contains("more than once", Assert.Single(Resolve("result", "result").Errors));
    }

    [Fact]
    public void A_choice_not_given_gets_its_default()
    {
        var resolution = Resolve("cqrs");

        Assert.Equal("handlers", Unit(resolution, "cqrs").Choices["dispatcher"]);
        Assert.Contains(("cqrs_dispatcher", "handlers"), resolution.TemplateArguments);
    }

    [Fact]
    public void A_given_choice_is_used()
    {
        var resolution = Resolve("cqrs:dispatcher=mediator");

        Assert.True(resolution.IsResolved);
        Assert.Contains(("cqrs_dispatcher", "mediator"), resolution.TemplateArguments);
        Assert.Empty(resolution.Licenses);
    }

    [Fact]
    public void An_invalid_choice_value_lists_the_values()
    {
        var error = Assert.Single(Resolve("cqrs:dispatcher=wolverine").Errors);

        Assert.Contains("handlers, mediator, mediatr", error);
    }

    [Fact]
    public void An_unknown_choice_lists_the_choices()
    {
        Assert.Contains("Its choices: dispatcher", Assert.Single(Resolve("cqrs:bus=x").Errors));
    }

    [Fact]
    public void A_choice_on_an_option_without_choices_is_rejected()
    {
        Assert.Contains("has no choices", Assert.Single(Resolve("result:x=y").Errors));
    }

    [Fact]
    public void Licenses_come_from_options_and_choice_values()
    {
        var resolution = Resolve("seq", "cqrs:dispatcher=mediatr");

        Assert.Equal(
            ["Seq is free for one user.", "MediatR needs a license key."],
            resolution.Licenses.Select(l => l.License.Notice).Order(StringComparer.Ordinal).Reverse());
        Assert.Contains(resolution.Licenses, l => l.Subject == "CQRS (MediatR)" && l.License.Setting == "MediatR:LicenseKey");
    }

    [Fact]
    public void Two_options_of_the_same_group_are_rejected()
    {
        var error = Assert.Single(Resolve("rabbitmq", "servicebus").Errors);

        Assert.Contains("both are a queue", error);
    }

    [Fact]
    public void Picking_another_option_of_a_group_replaces_the_one_in_the_solution()
    {
        var resolution = Resolve(existing: ["sqlserver", "result"], requested: ["postgresql"]);

        Assert.True(resolution.IsResolved);
        Assert.Equal(["sqlserver"], resolution.Replaced.Select(u => u.Id));
        Assert.Equal(["result", "postgresql"], Ids(resolution));
        Assert.Equal(Origin.Existing, Unit(resolution, "result").Origin);
    }

    [Fact]
    public void A_requirement_a_hidden_unit_provides_adds_it_and_its_own_requirements()
    {
        var resolution = Resolve("outbox");

        Assert.True(resolution.IsResolved);
        Assert.Equal(Origin.Required, Unit(resolution, "integration-events").Origin);
        Assert.Equal(["outbox"], Unit(resolution, "integration-events").AddedBy);
        Assert.Equal(["integration-events"], Unit(resolution, "event-serializer").AddedBy);
    }

    [Fact]
    public void A_hidden_unit_records_every_unit_it_was_added_for()
    {
        var resolution = Resolve("rabbitmq", "outbox");

        Assert.Equal(["outbox", "rabbitmq"], Unit(resolution, "integration-events").AddedBy.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_requirement_only_options_provide_is_unmet_and_lists_them()
    {
        var resolution = Resolve("inbox");

        Assert.False(resolution.IsResolved);
        Assert.Empty(resolution.Errors);
        var unmet = Assert.Single(resolution.Unmet);
        Assert.Equal("at-least-once-delivery", unmet.Capability);
        Assert.Equal(["outbox", "rabbitmq", "servicebus"], unmet.Providers.Select(p => p.Id));
        Assert.Equal(
            "Inbox needs at least once delivery, provided by Outbox (outbox), RabbitMQ (rabbitmq) or Azure Service Bus (servicebus).",
            unmet.Message);
    }

    [Theory]
    [InlineData("outbox")]
    [InlineData("rabbitmq")]
    public void A_requirement_is_met_by_any_provider(string provider)
    {
        Assert.True(Resolve("inbox", provider).IsResolved);
    }

    [Fact]
    public void A_requirement_nothing_provides_is_an_error()
    {
        var catalog = FakeCatalog.Build("""
            [{ "id": "x", "kind": "option", "concept": "pattern", "title": "X", "description": "d", "requires": ["magic"] }]
            """);

        var resolution = Resolver.Resolve(catalog, [], [OptionRequest.Of("x")], samples: false);

        Assert.Contains("nothing in the catalog provides", Assert.Single(resolution.Errors));
    }

    [Fact]
    public void Conflicts_are_reported_once_whichever_side_declares_them()
    {
        var error = Assert.Single(Resolve("cqrs", "legacy").Errors);

        Assert.Equal("CQRS and Legacy can't be used together.", error);
    }

    [Fact]
    public void An_option_without_sample_support_is_rejected_with_samples()
    {
        Assert.True(Resolve(existing: [], requested: ["legacy"], samples: false).IsResolved);

        var error = Assert.Single(Resolve(existing: [], requested: ["legacy"], samples: true).Errors);
        Assert.Contains("doesn't support the sample features", error);
    }

    [Fact]
    public void A_glue_unit_is_added_when_all_its_units_are_selected()
    {
        Assert.DoesNotContain("signalr-redis", Ids(Resolve("signalr")));

        var resolution = Resolve("signalr", "redis");

        Assert.Equal(Origin.Glue, Unit(resolution, "signalr-redis").Origin);
        Assert.Equal(["signalr", "redis"], Unit(resolution, "signalr-redis").AddedBy);
    }

    [Fact]
    public void Requesting_an_option_the_solution_has_changes_nothing()
    {
        var resolution = Resolve(existing: ["cqrs:dispatcher=mediator"], requested: ["cqrs"]);

        Assert.True(resolution.IsResolved);
        Assert.Equal(Origin.Existing, Unit(resolution, "cqrs").Origin);
        Assert.Equal("mediator", Unit(resolution, "cqrs").Choices["dispatcher"]);
    }

    [Fact]
    public void Changing_the_choice_of_an_applied_option_is_rejected()
    {
        Assert.Contains("changing a choice isn't supported", Assert.Single(Resolve(existing: ["cqrs:dispatcher=mediator"], requested: ["cqrs:dispatcher=handlers"]).Errors));
    }

    [Fact]
    public void An_option_the_template_no_longer_knows_is_an_error()
    {
        Assert.Contains("doesn't know", Assert.Single(Resolve(existing: ["gone"], requested: []).Errors));
    }

    [Theory]
    [InlineData("outbox")]
    [InlineData("rabbitmq")]
    public void A_recommended_option_is_suggested_once_the_selection_meets_its_requirements(string provider)
    {
        var resolution = Resolve(provider);

        Assert.Equal(["inbox"], resolution.Suggestions.Select(s => s.Id));
        Assert.DoesNotContain("inbox", Ids(resolution));
    }

    [Fact]
    public void Nothing_is_suggested_when_requirements_arent_met_or_it_is_selected()
    {
        Assert.Empty(Resolve("result").Suggestions);
        Assert.Empty(Resolve("outbox", "inbox").Suggestions);
        Assert.Empty(Resolve(existing: ["outbox", "inbox"], requested: ["result"]).Suggestions);
    }

    [Fact]
    public void A_solution_that_already_meets_the_requirements_gets_the_suggestion_again()
    {
        // Declining isn't recorded, so a later add offers it again.
        Assert.Equal(["inbox"], Resolve(existing: ["outbox"], requested: ["result"]).Suggestions.Select(s => s.Id));
    }

    [Fact]
    public void A_configuration_with_errors_gets_no_suggestions()
    {
        Assert.Empty(Resolve("outbox", "rabbitmq", "servicebus").Suggestions);
    }

    [Fact]
    public void Missing_for_annotates_options_that_need_another_option()
    {
        var inbox = FakeCatalog.World.Find("inbox")!;

        Assert.Equal("at-least-once-delivery", Assert.Single(Resolver.MissingFor(FakeCatalog.World, inbox, [])).Capability);
        Assert.Empty(Resolver.MissingFor(FakeCatalog.World, inbox, ["outbox"]));
    }

    [Fact]
    public void Missing_for_ignores_what_hidden_units_provide()
    {
        Assert.Empty(Resolver.MissingFor(FakeCatalog.World, FakeCatalog.World.Find("outbox")!, []));
    }
}
