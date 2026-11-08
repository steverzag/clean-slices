#if (servicebus)
using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CleanSlices.Infrastructure.IntegrationEvents.Broker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanSlices.Infrastructure.IntegrationEvents.ServiceBus;

// Receives this app's subscription and runs the handlers. Completed once they all succeed; otherwise abandoned,
// so Service Bus delivers it again, until the subscription's MaxDeliveryCount sends it to the dead-letter queue.
internal sealed class ServiceBusConsumer(
	ServiceBusClient client,
	IntegrationEventSerializer serializer,
	IntegrationEventHandlers handlers,
	IOptions<BrokerOptions> options,
	IHostEnvironment environment,
	ILogger<ServiceBusConsumer> logger) : BackgroundService
{
	// The app's own source name is already traced by ServiceDefaults.
	private readonly ActivitySource _activities = new(environment.ApplicationName);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var settings = options.Value;
		await using var processor = client.CreateProcessor(settings.Topic, settings.Subscription, new ServiceBusProcessorOptions
		{
			AutoCompleteMessages = false,
			PrefetchCount = settings.Prefetch,
		});

		processor.ProcessMessageAsync += ReceiveAsync;
		processor.ProcessErrorAsync += args =>
		{
			logger.LogError(args.Exception, "Receiving from Service Bus failed ({Source}); it keeps trying", args.ErrorSource);
			return Task.CompletedTask;
		};

		await processor.StartProcessingAsync(stoppingToken);
		try
		{
			await Task.Delay(Timeout.Infinite, stoppingToken);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
		}

		await processor.StopProcessingAsync(CancellationToken.None);
	}

	private async Task ReceiveAsync(ProcessMessageEventArgs args)
	{
		var received = args.Message;
		var name = received.Subject ?? string.Empty;
		var traceParent = received.ApplicationProperties.TryGetValue(BrokerMessage.TraceParentHeader, out var header) ? header as string : null;
		using var activity = _activities.StartActivity($"receive {name}", ActivityKind.Consumer, traceParent);
		activity?.SetTag("messaging.message.id", received.MessageId);

		IntegrationEventMessage message;
		try
		{
			var integrationEvent = serializer.Deserialize(name, received.Body.ToString());
			message = new IntegrationEventMessage(Guid.Parse(received.MessageId), integrationEvent);
		}
		catch (Exception ex) when (ex is UnknownIntegrationEventException or JsonException or FormatException)
		{
			// Delivering it again can't fix a message that can't be read.
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			logger.LogError(ex, "Integration event {Name} {Id} can't be read; it goes to the dead-letter queue", name, received.MessageId);
			await args.DeadLetterMessageAsync(received, "Unreadable", ex.Message, args.CancellationToken);
			return;
		}

		try
		{
			await handlers.RunAsync(message, args.CancellationToken);
			await args.CompleteMessageAsync(received, args.CancellationToken);
		}
		catch (Exception ex) when (!args.CancellationToken.IsCancellationRequested)
		{
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			logger.LogWarning(ex, "Integration event {Name} {Id} failed; Service Bus delivers it again until MaxDeliveryCount", name, message.Id);
			await args.AbandonMessageAsync(received, cancellationToken: args.CancellationToken);
		}
	}

	public override void Dispose()
	{
		_activities.Dispose();
		base.Dispose();
	}
}
#endif
