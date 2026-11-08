using CleanSlices.Cli.Catalog;

namespace CleanSlices.Cli.Tests.Catalog;

/// <summary>Catalogs for tests, written as JSON so they read like the real catalog.json.</summary>
internal static class FakeCatalog
{
    public const string Concepts = """
        {
          "pattern": { "title": "Patterns", "select": "many" },
          "infrastructure": { "title": "Infrastructure", "select": "onePerGroup" },
          "strategy": { "title": "Strategies", "select": "many" }
        }
        """;

    /// <summary>A small version of the planned catalog, with every kind of relation in it.</summary>
    public static OptionCatalog World { get; } = Build(
        units: """
        [
          { "id": "result", "kind": "option", "concept": "pattern", "title": "Result<T>", "description": "d" },
          { "id": "outbox", "kind": "option", "concept": "pattern", "title": "Outbox", "description": "d",
            "requires": ["integration-events"], "provides": ["at-least-once-delivery"], "addsTables": true, "addNote": "convert existing entities by hand" },
          { "id": "inbox", "kind": "option", "concept": "pattern", "title": "Inbox", "description": "d",
            "requires": ["at-least-once-delivery"], "recommended": true, "addsTables": true },
          { "id": "cqrs", "kind": "option", "concept": "pattern", "title": "CQRS", "description": "d",
            "choices": { "dispatcher": { "default": "handlers", "values": {
              "handlers": { "title": "Own handlers" },
              "mediator": { "title": "Mediator" },
              "mediatr": { "title": "MediatR", "license": { "notice": "MediatR needs a license key.", "setting": "MediatR:LicenseKey" } } } } } },
          { "id": "legacy", "kind": "option", "concept": "pattern", "title": "Legacy", "description": "d",
            "conflicts": ["cqrs"], "samples": false },
          { "id": "audit", "kind": "option", "concept": "strategy", "title": "Audit", "description": "d",
            "requires": ["current-user"] },
          { "id": "sqlserver", "kind": "option", "concept": "infrastructure", "group": "database", "title": "SQL Server", "description": "d" },
          { "id": "postgresql", "kind": "option", "concept": "infrastructure", "group": "database", "title": "PostgreSQL", "description": "d" },
          { "id": "rabbitmq", "kind": "option", "concept": "infrastructure", "group": "queue", "title": "RabbitMQ", "description": "d",
            "requires": ["integration-events"], "provides": ["at-least-once-delivery"] },
          { "id": "servicebus", "kind": "option", "concept": "infrastructure", "group": "queue", "title": "Azure Service Bus", "description": "d",
            "requires": ["integration-events"], "provides": ["at-least-once-delivery"] },
          { "id": "redis", "kind": "option", "concept": "infrastructure", "group": "cache", "title": "Redis", "description": "d" },
          { "id": "signalr", "kind": "option", "concept": "infrastructure", "group": "realtime", "title": "SignalR", "description": "d" },
          { "id": "seq", "kind": "option", "concept": "pattern", "title": "Seq", "description": "d",
            "license": { "notice": "Seq is free for one user." } },
          { "id": "integration-events", "kind": "hidden", "requires": ["event-serializer"] },
          { "id": "event-serializer", "kind": "hidden" },
          { "id": "current-user", "kind": "hidden" },
          { "id": "signalr-redis", "kind": "glue", "when": ["signalr", "redis"] }
        ]
        """,
        groups: """
        {
          "database": { "title": "Database", "required": true, "default": "sqlserver" },
          "queue": { "title": "Queue", "required": false },
          "cache": { "title": "Cache", "required": false },
          "realtime": { "title": "Realtime", "required": false }
        }
        """);

    public static OptionCatalog Build(string units, string groups = "{}", string concepts = Concepts) =>
        OptionCatalog.Parse($$"""
            { "catalogVersion": 1, "concepts": {{concepts}}, "groups": {{groups}}, "units": {{units}} }
            """);
}
