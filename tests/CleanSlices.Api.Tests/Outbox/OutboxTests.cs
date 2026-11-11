#if (outbox)
using System.Diagnostics;
using CleanSlices.Api.Tests.Infrastructure;
using CleanSlices.Api.Tests.IntegrationEvents;
using CleanSlices.Infrastructure.IntegrationEvents;
using CleanSlices.Infrastructure.Outbox;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CleanSlices.Api.Tests.Outbox;

public class OutboxTests(ApiFactory factory)
{
	private CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task A_published_event_is_saved_as_a_pending_row_with_the_trace()
	{
		var testId = Guid.NewGuid();
		using var activity = new Activity("test request").Start();

		await PublishAsync(new TestEvent(testId));

		var message = await FindAsync(testId);
		Assert.Equal(nameof(TestEvent), message.Name);
		Assert.Equal(activity.Id, message.TraceParent);
		Assert.Null(message.ProcessedAt);
		Assert.Equal(0, message.Attempts);
	}

	[Fact]
	public async Task A_published_event_isnt_saved_when_the_save_doesnt_happen()
	{
		var testId = Guid.NewGuid();

		await using (var scope = factory.Services.CreateAsyncScope())
		{
			await scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>().Publish(new TestEvent(testId), Ct);
		}

		Assert.Null(await FindOrDefaultAsync(testId));
	}

	[Fact]
	public async Task Processing_delivers_the_event_to_its_handlers_once()
	{
		var testId = Guid.NewGuid();
		await PublishAsync(new TestEvent(testId));

		var message = await ProcessUntilAsync(testId, m => m.ProcessedAt is not null);

		Assert.Equal(1, message.Attempts);
		Assert.Null(message.LockedBy);
		Assert.Equal(1, await RecordingHandler.WaitForDeliveriesAsync(testId, 1, Ct));
	}

#if (!message_broker)
	// In process only: through a broker, the outbox sees the broker accept the message, not the handlers fail.
	// The broker's own redeliveries are tested in IntegrationEvents/BrokerTests.cs.
	[Fact]
	public async Task A_failing_handler_is_retried_then_marked_failed()
	{
		var testId = Guid.NewGuid();
		await PublishAsync(new TestEvent(testId, Fail: true));
		var maxAttempts = factory.Services.GetRequiredService<IOptions<OutboxOptions>>().Value.MaxAttempts;

		var message = await ProcessUntilAsync(testId, m => m.FailedAt is not null);

		Assert.Equal(maxAttempts, message.Attempts);
		Assert.Null(message.ProcessedAt);
		Assert.Contains($"Failing on purpose for {testId}", message.LastError);

#if (inbox)
		// The other handler ran once: the inbox skips it on the retries, since it already succeeded.
		Assert.Equal(1, RecordingHandler.DeliveriesOf(testId));
#else
		// The other handler still ran on every attempt: one failing handler doesn't stop the rest.
		Assert.Equal(maxAttempts, RecordingHandler.DeliveriesOf(testId));
#endif
	}
#endif

	[Fact]
	public async Task Processors_claiming_at_the_same_time_handle_each_message_once()
	{
		var testIds = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();
		await using (var scope = factory.Services.CreateAsyncScope())
		{
			var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
			foreach (var testId in testIds)
			{
				await publisher.Publish(new TestEvent(testId), Ct);
			}

			await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync(Ct);
		}

		// Like several API instances: processors claiming batches in parallel until nothing is left.
		// Whether two claims really overlap depends on timing, so this can't prove the claim is safe; it catches gross mistakes.
		var processor = factory.Services.GetRequiredService<OutboxProcessor>();
		await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
		{
			while (await processor.ProcessBatchAsync(Ct) > 0)
			{
			}
		}, Ct)));

		foreach (var testId in testIds)
		{
			await ProcessUntilAsync(testId, m => m.ProcessedAt is not null);
		}

		foreach (var testId in testIds)
		{
			Assert.Equal(1, await RecordingHandler.WaitForDeliveriesAsync(testId, 1, Ct));
		}
	}

	[Fact]
	public async Task An_unknown_event_is_marked_failed_without_retrying()
	{
		var name = $"Deleted{Guid.NewGuid():N}";
		await using (var scope = factory.Services.CreateAsyncScope())
		{
			var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			context.Set<OutboxMessage>().Add(new OutboxMessage { Name = name, Payload = "{}" });
			await context.SaveChangesAsync(Ct);
		}

		var message = await ProcessUntilAsync(m => m.Name == name, m => m.FailedAt is not null);

		Assert.Equal(1, message.Attempts);
		Assert.Contains($"Unknown integration event '{name}'", message.LastError);
	}

	[Fact]
	public async Task An_event_that_cant_be_read_is_marked_failed_without_retrying()
	{
		var testId = Guid.NewGuid();
		await using (var scope = factory.Services.CreateAsyncScope())
		{
			var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			context.Set<OutboxMessage>().Add(new OutboxMessage { Name = nameof(UnreadableEvent), Payload = $$"""{"testId":"{{testId}}"}""" });
			await context.SaveChangesAsync(Ct);
		}

		var message = await ProcessUntilAsync(testId, m => m.FailedAt is not null);

		Assert.Equal(1, message.Attempts);
	}

	[Fact]
	public async Task Processed_rows_are_deleted_after_the_retention()
	{
		var testId = Guid.NewGuid();
		await PublishAsync(new TestEvent(testId));
		var message = await ProcessUntilAsync(testId, m => m.ProcessedAt is not null);

		await using (var scope = factory.Services.CreateAsyncScope())
		{
			var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			await context.Set<OutboxMessage>().Where(m => m.Id == message.Id)
				.ExecuteUpdateAsync(set => set.SetProperty(m => m.ProcessedAt, DateTimeOffset.UtcNow.AddDays(-8)), Ct);
		}

		await factory.Services.GetRequiredService<OutboxProcessor>().DeleteProcessedAsync(Ct);

		Assert.Null(await FindOrDefaultAsync(testId));
	}

	private async Task PublishAsync(IIntegrationEvent integrationEvent)
	{
		await using var scope = factory.Services.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>().Publish(integrationEvent, Ct);
		await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync(Ct);
	}

	private Task<OutboxMessage> ProcessUntilAsync(Guid testId, Func<OutboxMessage, bool> done) =>
		factory.ProcessOutboxUntilAsync(m => m.Payload.Contains(testId.ToString()), done, Ct);

	private Task<OutboxMessage> ProcessUntilAsync(System.Linq.Expressions.Expression<Func<OutboxMessage, bool>> which, Func<OutboxMessage, bool> done) =>
		factory.ProcessOutboxUntilAsync(which, done, Ct);

	private async Task<OutboxMessage> FindAsync(Guid testId) =>
		await FindOrDefaultAsync(testId) ?? throw new InvalidOperationException($"No outbox row for {testId}.");

	private Task<OutboxMessage?> FindOrDefaultAsync(Guid testId) =>
		factory.FindOutboxMessageAsync(m => m.Payload.Contains(testId.ToString()), Ct);
}
#endif
