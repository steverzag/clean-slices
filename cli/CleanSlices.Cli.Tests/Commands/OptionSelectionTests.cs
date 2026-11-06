using CleanSlices.Cli.Catalog;
using CleanSlices.Cli.Commands;
using CleanSlices.Cli.Tests.Catalog;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CleanSlices.Cli.Tests.Commands;

/// <summary>The interactive prompts, driven with scripted keys through Spectre's test console.</summary>
[Collection(nameof(ConsoleCollection))]
public sealed class OptionSelectionTests : IDisposable
{
    private static readonly OptionCatalog ResultOnly = FakeCatalog.Build("""
        [{ "id": "result", "kind": "option", "concept": "pattern", "title": "Result<T>", "description": "d" }]
        """);

    private readonly IAnsiConsole _original = AnsiConsole.Console;
    private readonly TestConsole _console = new();

    public OptionSelectionTests()
    {
        _console.Profile.Capabilities.Interactive = true;
        AnsiConsole.Console = _console;
    }

    public void Dispose()
    {
        AnsiConsole.Console = _original;
        _console.Dispose();
    }

    [Fact]
    public void Space_then_enter_selects_the_highlighted_option()
    {
        _console.Input.PushKey(ConsoleKey.Spacebar);
        _console.Input.PushKey(ConsoleKey.Enter);

        var requests = OptionSelection.Ask(ResultOnly, samples: false, applied: []);

        Assert.Equal("result", Assert.Single(requests).Id);
    }

    [Fact]
    public void Enter_alone_accepts_an_empty_selection()
    {
        _console.Input.PushKey(ConsoleKey.Enter);

        Assert.Empty(OptionSelection.Ask(ResultOnly, samples: false, applied: []));
    }

    [Fact]
    public void The_multi_select_says_which_key_selects()
    {
        _console.Input.PushKey(ConsoleKey.Enter);

        OptionSelection.Ask(ResultOnly, samples: false, applied: []);

        Assert.Contains("space selects, enter confirms", _console.Output);
    }

    [Fact]
    public void An_empty_plan_is_printed_too()
    {
        OptionSelection.PrintPlan(Resolver.Resolve(ResultOnly, [], [], samples: false), applied: []);

        Assert.Contains("Options: none (the default setup)", _console.Output);
    }

    [Fact]
    public void The_plan_lists_what_will_be_applied()
    {
        OptionSelection.PrintPlan(Resolver.Resolve(ResultOnly, [], [OptionRequest.Of("result")], samples: false), applied: []);

        Assert.Contains("+ Result<T>", _console.Output);
        Assert.DoesNotContain("none", _console.Output);
    }
}

[CollectionDefinition(nameof(ConsoleCollection), DisableParallelization = true)]
public sealed class ConsoleCollection;
