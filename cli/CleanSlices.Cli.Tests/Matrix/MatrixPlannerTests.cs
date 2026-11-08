using CleanSlices.Cli.Tests.Catalog;
using CleanSlices.Matrix;

namespace CleanSlices.Cli.Tests.Matrix;

public class MatrixPlannerTests
{
    private static readonly MatrixPlan Plan = MatrixPlanner.Plan(FakeCatalog.World);

    private static MatrixJob Job(string id) => Plan.Jobs.Single(j => j.Id == id);

    private static IEnumerable<string> Ids => Plan.Jobs.Select(j => j.Id);

    [Fact]
    public void The_default_runs_with_and_without_samples_on_every_push()
    {
        Assert.Equal(Tier.Push, Job("default").Tier);
        Assert.False(Job("default").Samples);
        Assert.True(Job("default-samples").Samples);
    }

    [Fact]
    public void Each_option_runs_alone_with_and_without_samples_and_added_later()
    {
        Assert.Contains("result", Ids);
        Assert.Contains("result-samples", Ids);
        Assert.Equal(Flow.AddLater, Job("result-later").Flow);
        Assert.Equal(Tier.Push, Job("result-later").Tier);
    }

    [Fact]
    public void Each_value_of_each_choice_gets_its_own_job()
    {
        Assert.Contains("cqrs", Ids);
        Assert.Contains("cqrs-dispatcher-mediator", Ids);
        Assert.Contains("cqrs-dispatcher-mediatr-samples", Ids);
        Assert.Equal("cqrs:dispatcher=mediator", Assert.Single(Job("cqrs-dispatcher-mediator").Options).ToString());
    }

    [Fact]
    public void An_option_without_sample_support_only_runs_without_samples()
    {
        Assert.Contains("legacy", Ids);
        Assert.DoesNotContain("legacy-samples", Ids);
        Assert.False(Job("legacy-later").Samples);
    }

    [Fact]
    public void An_option_that_needs_another_gets_its_first_provider()
    {
        Assert.Equal(["inbox", "outbox"], Job("inbox").Options.Select(o => o.Id));
    }

    [Fact]
    public void Pairs_run_in_the_full_tier_and_impossible_ones_are_skipped_with_the_reason()
    {
        Assert.Equal(Tier.Full, Job("result+outbox").Tier);
        Assert.True(Job("result+outbox").Samples);

        Assert.Contains(Plan.Skipped, s => s.Id == "rabbitmq+servicebus" && s.Reason.Contains("both are a queue", StringComparison.Ordinal));
        Assert.Contains(Plan.Skipped, s => s.Id == "cqrs+legacy" && s.Reason.Contains("can't be used together", StringComparison.Ordinal));
    }

    [Fact]
    public void Combinations_that_resolve_to_the_same_configuration_run_once()
    {
        // inbox alone already gets the outbox; sqlserver alone is the database default.
        Assert.DoesNotContain("outbox+inbox", Ids);
        Assert.DoesNotContain("sqlserver", Ids);
        Assert.Equal(Ids.Count(), Ids.Distinct().Count());
    }

    [Fact]
    public void Adding_a_group_default_later_isnt_a_job()
    {
        // Every solution already has the default database, so adding it later would add nothing.
        Assert.DoesNotContain("sqlserver-later", Ids);
        Assert.Contains("postgresql-later", Ids);
    }

    [Fact]
    public void Everything_prefers_a_groups_alternative_to_its_default()
    {
        var everything = Job("everything").Options.Select(o => o.Id).ToList();

        Assert.Contains("postgresql", everything);
        Assert.DoesNotContain("sqlserver", everything);
    }

    [Fact]
    public void Everything_holds_every_option_that_fits_and_drops_the_rest()
    {
        var everything = Job("everything").Options.Select(o => o.Id).ToList();

        Assert.Contains("result", everything);
        Assert.Contains("cqrs", everything);
        Assert.DoesNotContain("legacy", everything);                           // conflicts with cqrs, which came first
        Assert.Single(everything, id => id is "sqlserver" or "postgresql");    // one database
        Assert.Single(everything, id => id is "rabbitmq" or "servicebus");     // one queue
        Assert.Equal(Tier.Full, Job("everything").Tier);
    }


    [Fact]
    public void Listed_combinations_run_in_their_tier_with_samples()
    {
        var plan = MatrixPlanner.Plan(FakeCatalog.World, [new Combination(Tier.Push, "result", "cqrs:dispatcher=mediator")]);

        var job = plan.Jobs.Single(j => j.Id == "result+cqrs-dispatcher-mediator");
        Assert.Equal(Tier.Push, job.Tier);
        Assert.True(job.Samples);
        Assert.Equal(["result", "cqrs:dispatcher=mediator"], job.Options.Select(o => o.ToString()));
    }


    [Fact]
    public void A_listed_combination_that_is_also_a_pair_keeps_its_tier()
    {
        var plan = MatrixPlanner.Plan(FakeCatalog.World, [new Combination(Tier.Push, "outbox", "result")]);

        Assert.Equal(Tier.Push, plan.Jobs.Single(j => j.Id == "outbox+result").Tier);
        Assert.DoesNotContain(plan.Jobs, j => j.Id == "result+outbox");
    }
    [Fact]
    public void Listed_combinations_that_cant_be_resolved_are_skipped_with_the_reason()
    {
        var plan = MatrixPlanner.Plan(FakeCatalog.World, [new Combination(Tier.Push, "legacy", "cqrs"), new Combination(Tier.Push, "result", "nope")]);

        Assert.DoesNotContain(plan.Jobs, j => j.Id is "legacy+cqrs" or "result+nope");
        Assert.Contains(plan.Skipped, s => s.Id == "legacy+cqrs");
        Assert.Contains(plan.Skipped, s => s.Id == "result+nope");
    }
    [Fact]
    public void The_template_repository_is_built_once_per_set_of_symbols()
    {
        Assert.True(Job("result").RepositoryVariant);
        Assert.False(Job("result-samples").RepositoryVariant);   // same symbols
        Assert.False(Job("default").RepositoryVariant);          // the repository job covers it
        Assert.Contains("mediator", Job("cqrs-dispatcher-mediator").Symbols);
        Assert.Contains("integration_events", Job("outbox").Symbols);
    }
}
