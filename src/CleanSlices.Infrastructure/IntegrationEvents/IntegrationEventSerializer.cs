#if (integration_events)
using System.Text.Json;

namespace CleanSlices.Infrastructure.IntegrationEvents;

internal sealed class IntegrationEventSerializer(IntegrationEventTypes types)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public (string Name, string Payload) Serialize(IIntegrationEvent integrationEvent) =>
		(types.NameOf(integrationEvent), JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions));

	public IIntegrationEvent Deserialize(string name, string payload)
	{
		var type = types.Find(name) ?? throw new UnknownIntegrationEventException(name);
		return (IIntegrationEvent)(JsonSerializer.Deserialize(payload, type, JsonOptions)
			?? throw new JsonException($"The payload of integration event '{name}' is empty."));
	}
}

internal sealed class UnknownIntegrationEventException(string name)
	: Exception($"Unknown integration event '{name}'. Was the event class deleted or renamed? Keep the old name with [IntegrationEventName(\"{name}\")].")
{
	public string Name { get; } = name;
}
#endif
