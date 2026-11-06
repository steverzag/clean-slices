namespace CleanSlices.Cli.Catalog;

/// <summary>An option as the user asked for it: <c>result</c>, <c>cqrs:dispatcher=mediator</c>, <c>logging:seq=on,x=y</c>.</summary>
internal sealed record OptionRequest(string Id, IReadOnlyDictionary<string, string> Choices)
{
    public static OptionRequest Of(string id, params (string Choice, string Value)[] choices) =>
        new(id, choices.ToDictionary(c => c.Choice, c => c.Value, StringComparer.Ordinal));

    /// <returns>The request, or an error message saying what's wrong with <paramref name="spec"/>.</returns>
    public static (OptionRequest? Request, string? Error) Parse(string spec)
    {
        var text = spec.Trim();
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        var id = colon < 0 ? text : text[..colon];
        if (id.Length == 0)
        {
            return (null, $"'{spec}' has no option id. Expected <id> or <id>:<choice>=<value>[,<choice>=<value>].");
        }

        var choices = new Dictionary<string, string>(StringComparer.Ordinal);
        if (colon >= 0)
        {
            var list = text[(colon + 1)..];
            if (list.Length == 0)
            {
                return (null, $"'{spec}' has a ':' but no choices. Expected {id}:<choice>=<value>.");
            }

            foreach (var pair in list.Split(','))
            {
                var equals = pair.IndexOf('=', StringComparison.Ordinal);
                var choice = equals < 0 ? pair.Trim() : pair[..equals].Trim();
                var value = equals < 0 ? string.Empty : pair[(equals + 1)..].Trim();
                if (choice.Length == 0 || value.Length == 0)
                {
                    return (null, $"'{pair}' in '{spec}' isn't <choice>=<value>.");
                }

                if (!choices.TryAdd(choice, value))
                {
                    return (null, $"'{spec}' sets the choice '{choice}' twice.");
                }
            }
        }

        return (new OptionRequest(id, choices), null);
    }

    public override string ToString() =>
        Choices.Count == 0 ? Id : $"{Id}:{string.Join(",", Choices.Select(c => $"{c.Key}={c.Value}"))}";
}
