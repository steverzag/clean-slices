using CleanSlices.Cli.Catalog;

namespace CleanSlices.Matrix;

/// <summary>When a job runs: on every push and pull request, or only in the full (nightly, main, manual) run.</summary>
internal enum Tier
{
    Push,
    Full,
}

/// <summary>How a job builds its solution.</summary>
internal enum Flow
{
    /// <summary><c>new --with …</c>, then <c>add feature</c>.</summary>
    New,

    /// <summary><c>new</c> without the options, <c>add feature</c>, commit, then <c>add &lt;options&gt;</c>.</summary>
    AddLater,
}

/// <param name="Options">What the job passes to the CLI, providers of unmet requirements included.</param>
/// <param name="Symbols">The compiler symbols of the resolved configuration, for building the template repository itself.</param>
/// <param name="RepositoryVariant">Whether this job also builds and tests the template repository with <paramref name="Symbols"/>; one job per distinct set.</param>
internal sealed record MatrixJob(
    string Id,
    Tier Tier,
    Flow Flow,
    bool Samples,
    IReadOnlyList<OptionRequest> Options,
    IReadOnlyList<string> Symbols,
    bool RepositoryVariant)
{
    public string Describe() =>
        $"{(Flow == Flow.AddLater ? "add later" : "new")}{(Samples ? ", samples" : string.Empty)}"
        + (Options.Count == 0 ? ", no options" : $": {string.Join(" ", Options)}");
}

internal sealed record SkippedCombination(string Id, string Reason);

internal sealed record MatrixPlan(IReadOnlyList<MatrixJob> Jobs, IReadOnlyList<SkippedCombination> Skipped);

/// <summary>
/// The combinations CI tests, derived from the catalog through the resolver, so the matrix grows with the catalog:
/// the default with and without samples; each option alone, and each value of each of its choices, with and without
/// samples, plus the option added later; every pair with samples; one solution with everything that fits together.
/// Impossible combinations are skipped with the resolver's reason; options that need another option get its first
/// provider; combinations that resolve to the same configuration run once.
/// </summary>
internal static class MatrixPlanner
{
    public static MatrixPlan Plan(OptionCatalog catalog)
    {
        var jobs = new List<MatrixJob>();
        var skipped = new List<SkippedCombination>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var options = catalog.Units.Where(u => u.Kind == UnitKind.Option).ToList();

        var defaultConfiguration = Configuration(Resolver.Resolve(catalog, [], [], samples: false));

        void Add(string id, Tier tier, Flow flow, bool samples, IReadOnlyList<OptionRequest> requests)
        {
            if (Settle(catalog, requests, samples) is not { } settled)
            {
                var reason = Resolver.Resolve(catalog, [], requests, samples).Errors.FirstOrDefault() ?? "can't be resolved";
                skipped.Add(new SkippedCombination(id, reason));
                return;
            }

            var (finalRequests, resolution) = settled;
            var configuration = Configuration(resolution);

            // Adding later what every solution already has (a group's default) would add nothing.
            if (flow == Flow.AddLater && configuration == defaultConfiguration)
            {
                return;
            }

            if (!seen.Add($"{flow}|{samples}|{configuration}"))
            {
                return;
            }

            var symbols = resolution.Units
                .SelectMany(u => new[] { u.Unit.Symbol }.Concat(u.Choices.Values))
                .ToList();
            jobs.Add(new MatrixJob(id, tier, flow, samples, finalRequests, symbols, RepositoryVariant: false));
        }

        Add("default", Tier.Push, Flow.New, samples: false, []);
        Add("default-samples", Tier.Push, Flow.New, samples: true, []);

        foreach (var option in options)
        {
            foreach (var variant in Variants(option))
            {
                Add(Slug(variant), Tier.Push, Flow.New, samples: false, [variant]);
                if (option.Samples)
                {
                    Add($"{Slug(variant)}-samples", Tier.Push, Flow.New, samples: true, [variant]);
                }
            }

            Add($"{option.Id}-later", Tier.Push, Flow.AddLater, option.Samples, [OptionRequest.Of(option.Id)]);
        }

        for (var i = 0; i < options.Count; i++)
        {
            for (var j = i + 1; j < options.Count; j++)
            {
                var (a, b) = (options[i], options[j]);
                Add($"{a.Id}+{b.Id}", Tier.Full, Flow.New, a.Samples && b.Samples, [OptionRequest.Of(a.Id), OptionRequest.Of(b.Id)]);
            }
        }

        // Everything that fits together: each option in catalog order, kept when the configuration still resolves.
        // Group defaults go last: the resolver adds them anyway, and trying them first would shut out the alternatives.
        var defaults = catalog.Groups.Values.Select(g => g.Default).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var everything = new List<OptionRequest>();
        foreach (var option in options.OrderBy(o => defaults.Contains(o.Id) ? 1 : 0))
        {
            List<OptionRequest> candidate = [.. everything, OptionRequest.Of(option.Id)];
            if (Settle(catalog, candidate, samples: candidate.All(r => catalog.Find(r.Id)!.Samples)) is not null)
            {
                everything = candidate;
            }
        }

        if (everything.Count > 1)
        {
            Add("everything", Tier.Full, Flow.New, everything.All(r => catalog.Find(r.Id)!.Samples), everything);
        }

        // The template repository is built once per distinct set of compiler symbols; the default needs none.
        var built = new HashSet<string>(StringComparer.Ordinal);
        for (var k = 0; k < jobs.Count; k++)
        {
            var key = string.Join(";", jobs[k].Symbols.Order(StringComparer.Ordinal));
            if (jobs[k].Options.Count > 0 && built.Add(key))
            {
                jobs[k] = jobs[k] with { RepositoryVariant = true };
            }
        }

        return new MatrixPlan(jobs, skipped);
    }

    /// <summary>
    /// Resolves, adding the first provider of each unmet requirement until nothing is missing.
    /// Null when the combination is impossible (conflicts, two of a group, no sample support).
    /// </summary>
    internal static (IReadOnlyList<OptionRequest> Requests, Resolution Resolution)? Settle(
        OptionCatalog catalog,
        IReadOnlyList<OptionRequest> requests,
        bool samples)
    {
        var current = requests.ToList();
        for (var round = 0; round <= catalog.Units.Count; round++)
        {
            var resolution = Resolver.Resolve(catalog, [], current, samples);
            if (resolution.Errors.Count > 0)
            {
                return null;
            }

            if (resolution.Unmet.Count == 0)
            {
                return (current, resolution);
            }

            current.AddRange(resolution.Unmet
                .Select(m => m.Providers[0].Id)
                .Distinct()
                .Where(id => current.All(r => r.Id != id))
                .Select(id => OptionRequest.Of(id)));
        }

        return null;
    }

    /// <summary>The option with its defaults, then each non-default value of each choice on its own.</summary>
    private static IEnumerable<OptionRequest> Variants(Unit option)
    {
        yield return OptionRequest.Of(option.Id);
        foreach (var (name, choice) in option.Choices)
        {
            foreach (var value in choice.Values.Keys.Where(v => v != choice.Default))
            {
                yield return OptionRequest.Of(option.Id, (name, value));
            }
        }
    }

    /// <summary>The options a resolution ends with, choices included: two jobs with the same one test the same thing.</summary>
    private static string Configuration(Resolution resolution) =>
        string.Join(" ", resolution.Units.Where(u => u.Unit.Kind == UnitKind.Option).Select(u => new OptionRequest(u.Unit.Id, u.Choices).ToString()));

    private static string Slug(OptionRequest request) =>
        string.Join("-", new[] { request.Id }.Concat(request.Choices.SelectMany(c => new[] { c.Key, c.Value })));
}
