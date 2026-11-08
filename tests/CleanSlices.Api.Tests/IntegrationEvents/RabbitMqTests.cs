#if (rabbitmq)
using System.Collections.Concurrent;
using System.Text;
using CleanSlices.Api.Tests.Infrastructure;
using CleanSlices.Infrastructure.IntegrationEvents;
using CleanSlices.Infrastructure.IntegrationEvents.Broker;
using CleanSlices.Infrastructure.IntegrationEvents.RabbitMq;
#if (!outbox)
using CleanSlices.Infrastructure.Persistence;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CleanSlices.Api.Tests.IntegrationEvents;

// Against a real RabbitMQ container; the test host's consumer runs the handlers, so tests wait for them.
public class RabbitMqTests(ApiFactory factory)
{
	private static readonly ConcurrentDictionary<string, bool> DeadLettered = [];
	private static readonly SemaphoreSlim DeadLetterReader = new(1, 1);

	private CancellationToken Ct => TestContext.Current.CancellationToken;

	private BrokerOptions Options => factory.Services.GetRequiredService<IOptions<BrokerOptions>>().Value;

	[Fact]
	public async Task An_event_sent_to_the_broker_reaches_its_handlers()
	{
		var testId = Guid.NewGuid();

		await SendAsync(new TestEvent(testId));

		Assert.Equal(1, await RecordingHandler.WaitForDeliveriesAsync(testId, 1, Ct));
	}
#if (!outbox)

	[Fact]
	public async Task A_published_event_is_sent_once_the_save_commits()
	{
		var testId = Guid.NewGuid();
		await using var scope = factory.Services.CreateAsyncScope();

		await scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>().Publish(new TestEvent(testId), Ct);
		await Task.Delay(500, Ct);
		Assert.Equal(0, RecordingHandler.DeliveriesOf(testId));

		await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync(Ct);

		Assert.Equal(1, await RecordingHandler.WaitForDeliveriesAsync(testId, 1, Ct));
	}
#endif

	[Fact]
	public async Task A_failing_handler_gets_every_delivery_then_the_message_goes_to_the_dead_letter_queue()
	{
		var testId = Guid.NewGuid();
		var messageId = await SendAsync(new TestEvent(testId, Fail: true));

		Assert.True(await IsDeadLetteredAsync(messageId), "The message never reached the dead-letter queue.");
#if (inbox)
		// The other handler succeeded on the first delivery; the inbox skipped it on the redeliveries.
		Assert.Equal(1, RecordingHandler.DeliveriesOf(testId));
#else
		// The other handler ran on every delivery: one failing handler doesn't stop the rest.
		Assert.Equal(Options.MaxDeliveries, RecordingHandler.DeliveriesOf(testId));
#endif
	}

	[Fact]
	public async Task A_message_that_cant_be_read_goes_to_the_dead_letter_queue_at_once()
	{
		var messageId = Guid.NewGuid().ToString();
		var connection = factory.Services.GetRequiredService<IConnection>();
		await factory.Services.GetRequiredService<RabbitMqTopology>().EnsureAsync(Ct);

		await using (var channel = await connection.CreateChannelAsync(cancellationToken: Ct))
		{
			var properties = new BasicProperties { MessageId = messageId, Type = "NoSuchEvent", Persistent = true };
			await channel.BasicPublishAsync(Options.Topic, routingKey: nameof(TestEvent), mandatory: false, properties, Encoding.UTF8.GetBytes("{}"), Ct);
		}

		Assert.True(await IsDeadLetteredAsync(messageId), "The message never reached the dead-letter queue.");
	}

	private async Task<string> SendAsync(TestEvent testEvent)
	{
		var message = new IntegrationEventMessage(Guid.NewGuid(), testEvent);
		await factory.Services.GetRequiredService<IMessageDispatcher>().Dispatch(message, Ct);
		return message.Id.ToString();
	}

	// Tests share the dead-letter queue: whoever reads it records every message id it finds.
	private async Task<bool> IsDeadLetteredAsync(string messageId)
	{
		var connection = factory.Services.GetRequiredService<IConnection>();
		var deadline = DateTime.UtcNow.AddSeconds(30);
		while (!DeadLettered.ContainsKey(messageId) && DateTime.UtcNow < deadline)
		{
			await DeadLetterReader.WaitAsync(Ct);
			try
			{
				await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
				while (await channel.BasicGetAsync($"{Options.Subscription}.dead-letter", autoAck: true, Ct) is { } dead)
				{
					DeadLettered[dead.BasicProperties.MessageId ?? string.Empty] = true;
				}
			}
			finally
			{
				DeadLetterReader.Release();
			}

			await Task.Delay(100, Ct);
		}

		return DeadLettered.ContainsKey(messageId);
	}
}
#endif
