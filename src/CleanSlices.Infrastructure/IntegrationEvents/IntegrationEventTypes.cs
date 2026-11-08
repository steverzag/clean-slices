#if (integration_events)
using System.Reflection;

namespace CleanSlices.Infrastructure.IntegrationEvents;

internal sealed class IntegrationEventTypes
{
	private readonly Dictionary<string, Type> _byName;
	private readonly Dictionary<Type, string> _byType;

	public IntegrationEventTypes(IEnumerable<Type> eventTypes)
	{
		var types = eventTypes.Distinct().ToList();

		var duplicates = types.GroupBy(NameOf).Where(g => g.Count() > 1).ToList();
		if (duplicates.Count > 0)
		{
			throw new InvalidOperationException(
				"Integration events must have unique names. " +
				string.Join("; ", duplicates.Select(d => $"'{d.Key}' is used by {string.Join(" and ", d.Select(t => t.FullName))}")) +
				$". Rename one, or give it a name with [{nameof(IntegrationEventNameAttribute).Replace("Attribute", string.Empty, StringComparison.Ordinal)}].");
		}

		_byType = types.ToDictionary(t => t, NameOf);
		_byName = _byType.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);
	}

	public static IEnumerable<Type> FoundIn(Assembly assembly) =>
		assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IIntegrationEvent).IsAssignableFrom(t));

	public Type? Find(string name) => _byName.GetValueOrDefault(name);
#if (message_broker)

	public IReadOnlyCollection<string> Names => _byName.Keys;
#endif

	public string NameOf(IIntegrationEvent integrationEvent) =>
		_byType.TryGetValue(integrationEvent.GetType(), out var name)
			? name
			: throw new InvalidOperationException(
				$"{integrationEvent.GetType().FullName} isn't a known integration event. Define integration events in the Infrastructure project, or register a handler for it.");

	private static string NameOf(Type type) =>
		type.GetCustomAttribute<IntegrationEventNameAttribute>()?.Name ?? type.Name;
}
#endif
