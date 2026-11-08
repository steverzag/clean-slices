#if (servicebus)
using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using CleanSlices.Infrastructure.IntegrationEvents.Broker;
using Microsoft.Extensions.Options;

namespace CleanSlices.Infrastructure.IntegrationEvents.ServiceBus;

// Sends to the app's topic; when this returns, Service Bus has the message.
internal sealed class ServiceBusDispatcher(ServiceBusClient client, IntegrationEventSerializer serializer, IOptions<BrokerOptions> options)
	: IMessageDispatcher, IAsyncDisposable
{
	private readonly ServiceBusSender _sender = client.CreateSender(options.Value.Topic);

	public async Task Dispatch(IntegrationEventMessage message, CancellationToken ct)
	{
		var (name, payload) = serializer.Serialize(message.Event);
		var busMessage = new ServiceBusMessage(payload)
		{
			MessageId = message.Id.ToString(),
			Subject = name,
			ContentType = BrokerMessage.ContentType,
		};

		if (Activity.Current?.Id is { } traceParent)
		{
			busMessage.ApplicationProperties[BrokerMessage.TraceParentHeader] = traceParent;
		}

		await _sender.SendMessageAsync(busMessage, ct);
	}

	public ValueTask DisposeAsync() => _sender.DisposeAsync();
}
#endif
