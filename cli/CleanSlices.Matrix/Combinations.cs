namespace CleanSlices.Matrix;

/// <summary>A combination tested beyond the derived singles, pairs and "everything", written like <c>--with</c> values.</summary>
internal sealed record Combination(Tier Tier, params string[] Options)
{
    public string Id => string.Join("+", Options.Select(o => o.Replace(':', '-').Replace('=', '-')));
}

internal static class Combinations
{
    /// <summary>
    /// Options that meet in the same members (each slice handler has one variant per combination of them), and glue units.
    /// The combination where all of them meet runs on every push.
    /// </summary>
    public static IReadOnlyList<Combination> All { get; } =
    [
        new(Tier.Push, "result", "repository", "cqrs"),
        new(Tier.Full, "result", "repository", "cqrs:dispatcher=mediator"),
        new(Tier.Push, "fusioncache", "redis"),
    ];
}
