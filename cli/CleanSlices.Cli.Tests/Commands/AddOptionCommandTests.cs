using CleanSlices.Cli.Commands;
using CleanSlices.Cli.Tests.Catalog;

namespace CleanSlices.Cli.Tests.Commands;

public class AddOptionCommandTests
{
    [Theory]
    [InlineData("outbox", "AddOutbox")]
    [InlineData("outbox inbox", "AddOutboxInbox")]
    [InlineData("integration-events", "AddIntegrationEvents")]
    public void The_suggested_migration_is_named_after_the_units(string ids, string expected)
    {
        var units = ids.Split(' ').Select(id => FakeCatalog.World.Find(id)!);

        Assert.Equal(expected, AddOptionCommand.MigrationName(units));
    }
}
