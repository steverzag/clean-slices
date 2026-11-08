#if (rabbitmq)
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CleanSlices.Infrastructure.IntegrationEvents.Broker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CleanSlices.Infrastructure.IntegrationEvents.RabbitMq;

// Receives this app's queue and runs the handlers. Acknowledged once they all succeed; otherwise returned to
// the queue and delivered again, until the queue's delivery limit sends it to the dead-letter queue.
internal sealed class RabbitMqConsumer(
	IConnection connection,
	RabbitMqTopology topology,
	IntegrationEventSerializer serializer,
	IntegrationEventHandlers handlers,
	IOptions<BrokerOptions> options,
	IHostEnvironment environment,
	ILogger<RabbitMqConsumer> logger) : BackgroundService
{
	// The app's own source name is already traced by ServiceDefaults.
	private readonly ActivitySource _activities = new(environment.ApplicationName);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await topology.EnsureAsync(stoppingToken);

		await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
		await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: options.Value.Prefetch, global: false, stoppingToken);

		var consumer = new AsyncEventingBasicConsumer(channel);
		consumer.ReceivedAsync += (_, delivery) => ReceiveAsync(channel, delivery, stoppingToken);
		await channel.BasicConsumeAsync(topology.Queue, autoAck: false, consumer, stoppingToken);

		try
		{
			await Task.Delay(Timeout.Infinite, stoppingToken);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
		}
	}

	private async Task ReceiveAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
	{
		var properties = delivery.BasicProperties;
		var name = properties.Type ?? string.Empty;
		var traceParent = properties.Headers is { } headers
			&& headers.TryGetValue(BrokerMessage.TraceParentHeader, out var header) && header is byte[] bytes
			? Encoding.UTF8.GetString(bytes)
			: null;
		using var activity = _activities.StartActivity($"receive {name}", ActivityKind.Consumer, traceParent);
		activity?.SetTag("messaging.message.id", properties.MessageId);

		IntegrationEventMessage message;
		try
		{
			var integrationEvent = serializer.Deserialize(name, Encoding.UTF8.GetString(delivery.Body.Span));
			message = new IntegrationEventMessage(Guid.Parse(properties.MessageId ?? string.Empty), integrationEvent);
		}
		catch (Exception ex) when (ex is UnknownIntegrationEventException or JsonException or FormatException)
		{
			// Delivering it again can't fix a message that can't be read.
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			logger.LogError(ex, "Integration event {Name} {Id} can't be read; it goes to the dead-letter queue", name, properties.MessageId);
			await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, ct);
			return;
		}

		try
		{
			await handlers.RunAsync(message, ct);
			await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
		}
		catch (Exception ex) when (!ct.IsCancellationRequested)
		{
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			logger.LogWarning(ex, "Integration event {Name} {Id} failed; RabbitMQ delivers it again until its delivery limit", name, message.Id);
			await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, ct);
		}
	}

	public override void Dispose()
	{
		_activities.Dispose();
		base.Dispose();
	}
}
#endif
