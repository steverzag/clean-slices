#if (message_broker)
namespace CleanSlices.Infrastructure.IntegrationEvents.Broker;

// The message format on every broker: the JSON payload as the body, the event name as the message type
// (and RabbitMQ's routing key), the message id (the outbox row's, when there is one) and the trace parent.
internal static class BrokerMessage
{
	public const string ContentType = "application/json";
	public const string TraceParentHeader = "traceparent";
}
#endif
