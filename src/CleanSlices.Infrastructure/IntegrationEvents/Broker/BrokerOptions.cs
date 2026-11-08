#if (message_broker)
namespace CleanSlices.Infrastructure.IntegrationEvents.Broker;

internal sealed class BrokerOptions
{
	public const string Section = "Messaging";

	// Where this app publishes its integration events (a RabbitMQ exchange, a Service Bus topic).
	public string Topic { get; set; } = "cleanslices-events";

	// Where this app receives them (a RabbitMQ queue, a Service Bus subscription).
	public string Subscription { get; set; } = "cleanslices-api";

	// Deliveries of one message before it goes to the dead-letter queue. RabbitMQ only: on Service Bus
	// it's the subscription's MaxDeliveryCount, set in the AppHost.
	public int MaxDeliveries { get; set; } = 5;

	public ushort Prefetch { get; set; } = 10;
}
#endif
