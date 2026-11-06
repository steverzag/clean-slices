using CleanSlices.Cli.Catalog;

namespace CleanSlices.Cli.Tests.Catalog;

public class OptionRequestTests
{
    [Fact]
    public void An_id_alone_has_no_choices()
    {
        var (request, error) = OptionRequest.Parse("result");

        Assert.Null(error);
        Assert.Equal("result", request!.Id);
        Assert.Empty(request.Choices);
    }

    [Fact]
    public void Choices_follow_a_colon_separated_by_commas()
    {
        var (request, _) = OptionRequest.Parse("logging:seq=on,format=json");

        Assert.Equal("logging", request!.Id);
        Assert.Equal("on", request.Choices["seq"]);
        Assert.Equal("json", request.Choices["format"]);
        Assert.Equal("logging:seq=on,format=json", request.ToString());
    }

    [Theory]
    [InlineData("", "no option id")]
    [InlineData(":a=b", "no option id")]
    [InlineData("cqrs:", "no choices")]
    [InlineData("cqrs:dispatcher", "isn't <choice>=<value>")]
    [InlineData("cqrs:=mediator", "isn't <choice>=<value>")]
    [InlineData("cqrs:dispatcher=", "isn't <choice>=<value>")]
    [InlineData("cqrs:dispatcher=a,dispatcher=b", "twice")]
    public void Malformed_specs_say_what_is_wrong(string spec, string expected)
    {
        var (request, error) = OptionRequest.Parse(spec);

        Assert.Null(request);
        Assert.Contains(expected, error);
    }
}
