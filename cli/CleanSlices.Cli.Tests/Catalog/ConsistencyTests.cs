using System.Text.Json.Nodes;
using CleanSlices.Cli.Catalog;

namespace CleanSlices.Cli.Tests.Catalog;

/// <summary>
/// The real catalog.json against the real template.json files: the catalog says what options exist,
/// the templates render them, and these tests keep the two in step.
/// </summary>
public class ConsistencyTests
{
    private static readonly string Root = FindRepositoryRoot();

    private static readonly OptionCatalog Catalog = OptionCatalog.Parse(File.ReadAllText(Path.Combine(Root, OptionCatalog.FileName)));

    private static readonly JsonObject SolutionSymbols = Symbols(".template.config/template.json");

    private static readonly JsonObject FeatureSymbols = Symbols("templates/cs-feature/.template.config/template.json");

    /// <summary>What decides how a symbol renders; descriptions may differ per template.</summary>
    private static readonly string[] RenderingProperties = ["type", "datatype", "defaultValue", "choices", "value"];

    [Fact]
    public void The_catalog_passes_every_cross_reference_rule()
    {
        Assert.Empty(CatalogValidator.Validate(Catalog));
    }

    [Fact]
    public void The_solution_template_has_a_bool_parameter_for_every_unit()
    {
        foreach (var unit in Catalog.Units)
        {
            var symbol = SolutionSymbols[unit.Symbol];
            Assert.True(symbol is not null, $"template.json has no symbol '{unit.Symbol}' for unit '{unit.Id}'.");
            AssertBoolParameter(unit.Symbol, symbol!);
        }
    }

    [Fact]
    public void The_solution_template_has_every_choice_with_a_computed_bool_per_value()
    {
        foreach (var unit in Catalog.Units)
        {
            foreach (var (name, choice) in unit.Choices)
            {
                AssertChoice(SolutionSymbols, unit, name, choice);
            }
        }
    }

    [Fact]
    public void The_feature_template_only_defines_catalog_symbols_and_defines_them_like_the_solution_template()
    {
        // The CLI passes a template only the parameters it defines, so the feature template declares just the
        // options its code depends on. What it declares must exist in the catalog and match the solution template.
        foreach (var (name, symbol) in OptionSymbols(FeatureSymbols))
        {
            Assert.True(SolutionSymbols[name] is not null, $"cs-feature defines '{name}', which the solution template doesn't.");
            foreach (var property in RenderingProperties)
            {
                Assert.True(
                    JsonNode.DeepEquals(SolutionSymbols[name]![property], symbol[property]),
                    $"cs-feature defines '{property}' of '{name}' differently from the solution template.");
            }
        }
    }

    [Theory]
    [InlineData(".template.config/template.json")]
    [InlineData("templates/cs-feature/.template.config/template.json")]
    public void Templates_have_no_option_symbols_the_catalog_doesnt_know(string templateJson)
    {
        var known = Catalog.Units.SelectMany(CatalogValidator.SymbolsOf).ToHashSet(StringComparer.Ordinal);

        foreach (var (name, _) in OptionSymbols(Symbols(templateJson)))
        {
            Assert.True(known.Contains(name), $"{templateJson} defines '{name}', which isn't a symbol of any catalog unit (typo, or missing from catalog.json?).");
        }
    }

    /// <summary>Bool and choice parameters and computed symbols, minus the template's own parameters.</summary>
    private static IEnumerable<(string Name, JsonNode Symbol)> OptionSymbols(JsonObject symbols) =>
        symbols
            .Where(s => s.Value?["type"]?.GetValue<string>() is "computed"
                || (s.Value?["type"]?.GetValue<string>() is "parameter" && s.Value?["datatype"]?.GetValue<string>() is "bool" or "choice"))
            .Where(s => !CatalogValidator.ReservedSymbols.Contains(s.Key))
            .Select(s => (s.Key, s.Value!));

    private static void AssertBoolParameter(string name, JsonNode symbol)
    {
        Assert.True(symbol["type"]?.GetValue<string>() == "parameter", $"'{name}' must be a parameter.");
        Assert.True(symbol["datatype"]?.GetValue<string>() == "bool", $"'{name}' must be a bool.");
        Assert.True(symbol["defaultValue"]?.GetValue<string>() == "false", $"'{name}' must default to false: options are off unless the resolver selects them.");
    }

    private static void AssertChoice(JsonObject symbols, Unit unit, string name, Choice choice)
    {
        var parameterName = unit.ChoiceParameter(name);
        var parameter = symbols[parameterName];
        Assert.True(parameter is not null, $"template.json has no parameter '{parameterName}' for choice '{name}' of '{unit.Id}'.");
        Assert.True(parameter!["datatype"]?.GetValue<string>() == "choice", $"'{parameterName}' must be a choice parameter.");
        Assert.True(parameter["defaultValue"]?.GetValue<string>() == choice.Default, $"'{parameterName}' must default to '{choice.Default}', like the catalog.");

        var values = parameter["choices"]!.AsArray().Select(c => c!["choice"]!.GetValue<string>()).ToList();
        Assert.True(values.SequenceEqual(choice.Values.Keys), $"'{parameterName}' has values [{string.Join(", ", values)}]; the catalog has [{string.Join(", ", choice.Values.Keys)}].");

        foreach (var value in choice.Values.Keys)
        {
            var computed = symbols[value];
            var expected = $"({parameterName} == \"{value}\")";
            Assert.True(computed?["type"]?.GetValue<string>() == "computed" && computed["value"]?.GetValue<string>() == expected,
                $"template.json needs a computed symbol '{value}' with value '{expected}', so code can use #if ({value}).");
        }
    }

    private static JsonObject Symbols(string templateJson) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Root, templateJson)))!["symbols"]!.AsObject();

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, OptionCatalog.FileName))
                && Directory.Exists(Path.Combine(directory.FullName, ".template.config")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No {OptionCatalog.FileName} found above {AppContext.BaseDirectory}.");
    }
}
