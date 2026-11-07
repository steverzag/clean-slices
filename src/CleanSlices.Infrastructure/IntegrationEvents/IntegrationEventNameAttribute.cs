#if (integration_events)
namespace CleanSlices.Infrastructure.IntegrationEvents;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventNameAttribute(string name) : Attribute
{
	public string Name { get; } = name;
}
#endif
