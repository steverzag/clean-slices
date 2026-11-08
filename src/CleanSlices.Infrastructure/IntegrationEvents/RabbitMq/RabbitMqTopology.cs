#if (rabbitmq)
using CleanSlices.Infrastructure.IntegrationEvents.Broker;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CleanSlices.Infrastructure.IntegrationEvents.RabbitMq;

// Declared once, by whichever comes first: publishing or consuming. Without the queue and its bindings,
// RabbitMQ drops what's published to the exchange.
internal sealed class RabbitMqTopology(IConnection connection, IntegrationEventTypes types, IOptions<BrokerOptions> options) : IDisposable
{
	private readonly SemaphoreSlim _lock = new(1, 1);
	private bool _declared;

	public string Exchange => options.Value.Topic;

	public string Queue => options.Value.Subscription;

	public async Task EnsureAsync(CancellationToken ct)
	{
		if (_declared)
		{
			return;
		}

		await _lock.WaitAsync(ct);
		try
		{
			if (_declared)
			{
				return;
			}

			var settings = options.Value;
			var deadLetters = $"{settings.Subscription}.dead-letter";
			await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

			await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
			await channel.ExchangeDeclareAsync(deadLetters, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: ct);
			await channel.QueueDeclareAsync(deadLetters, durable: true, exclusive: false, autoDelete: false,
				arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: ct);
			await channel.QueueBindAsync(deadLetters, deadLetters, routingKey: string.Empty, cancellationToken: ct);

			// A quorum queue counts deliveries itself: past the limit, the message goes to the dead-letter queue.
			await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
				arguments: new Dictionary<string, object?>
				{
					["x-queue-type"] = "quorum",
					["x-delivery-limit"] = settings.MaxDeliveries - 1,
					["x-dead-letter-exchange"] = deadLetters,
				},
				cancellationToken: ct);

			// One binding per integration event this app knows: the routing key is the event's name.
			foreach (var name in types.Names)
			{
				await channel.QueueBindAsync(Queue, Exchange, routingKey: name, cancellationToken: ct);
			}

			_declared = true;
		}
		finally
		{
			_lock.Release();
		}
	}

	public void Dispose() => _lock.Dispose();
}
#endif
