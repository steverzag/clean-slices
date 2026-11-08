#if (rabbitmq)
using System.Diagnostics;
using System.Text;
using CleanSlices.Infrastructure.IntegrationEvents.Broker;
using RabbitMQ.Client;

namespace CleanSlices.Infrastructure.IntegrationEvents.RabbitMq;

// Sends to the exchange and waits for the broker's confirmation: when this returns, RabbitMQ has the message.
internal sealed class RabbitMqDispatcher(IConnection connection, RabbitMqTopology topology, IntegrationEventSerializer serializer) : IMessageDispatcher
{
	private static readonly CreateChannelOptions Confirmed = new(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

	public async Task Dispatch(IntegrationEventMessage message, CancellationToken ct)
	{
		await topology.EnsureAsync(ct);

		var (name, payload) = serializer.Serialize(message.Event);
		var properties = new BasicProperties
		{
			MessageId = message.Id.ToString(),
			Type = name,
			ContentType = BrokerMessage.ContentType,
			Persistent = true,
			Headers = new Dictionary<string, object?> { [BrokerMessage.TraceParentHeader] = Activity.Current?.Id },
		};

		await using var channel = await connection.CreateChannelAsync(Confirmed, ct);
		await channel.BasicPublishAsync(topology.Exchange, routingKey: name, mandatory: false, properties, Encoding.UTF8.GetBytes(payload), ct);
	}
}
#endif
