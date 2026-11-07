#if (outbox)
using System.Diagnostics;
using CleanSlices.Api.Tests.Infrastructure;
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

		await PublishAsync(new OutboxTestEvent(testId));

		var message = await FindAsync(testId);
		Assert.Equal(nameof(OutboxTestEvent), message.Name);
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
			await scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>().Publish(new OutboxTestEvent(testId), Ct);
		}

		Assert.Null(await FindOrDefaultAsync(testId));
	}

	[Fact]
	public async Task Processing_delivers_the_event_to_its_handlers_once()
	{
		var testId = Guid.NewGuid();
		await PublishAsync(new OutboxTestEvent(testId));

		var message = await ProcessUntilAsync(testId, m => m.ProcessedAt is not null);

		Assert.Equal(1, message.Attempts);
		Assert.Null(message.LockedBy);
		Assert.Equal(1, RecordingHandler.DeliveriesOf(testId));
	}

	[Fact]
	public async Task A_failing_handler_is_retried_then_marked_failed()
	{
		var testId = Guid.NewGuid();
		await PublishAsync(new OutboxTestEvent(testId, Fail: true));
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
	public async Task Processed_rows_are_deleted_after_the_retention()
	{
		var testId = Guid.NewGuid();
		await PublishAsync(new OutboxTestEvent(testId));
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
		ProcessUntilAsync(m => m.Payload.Contains(testId.ToString()), done);

	// Other tests process the same table at the same time, so whoever's batch picks the row up, this waits for its state.
	private async Task<OutboxMessage> ProcessUntilAsync(System.Linq.Expressions.Expression<Func<OutboxMessage, bool>> which, Func<OutboxMessage, bool> done)
	{
		var processor = factory.Services.GetRequiredService<OutboxProcessor>();
		for (var round = 0; round < 50; round++)
		{
			await processor.ProcessBatchAsync(Ct);
			var message = await FindOrDefaultAsync(which);
			if (message is not null && done(message))
			{
				return message;
			}
		}

		throw new TimeoutException("The outbox row never reached the expected state.");
	}

	private async Task<OutboxMessage> FindAsync(Guid testId) =>
		await FindOrDefaultAsync(testId) ?? throw new InvalidOperationException($"No outbox row for {testId}.");

	private Task<OutboxMessage?> FindOrDefaultAsync(Guid testId) =>
		FindOrDefaultAsync(m => m.Payload.Contains(testId.ToString()));

	private async Task<OutboxMessage?> FindOrDefaultAsync(System.Linq.Expressions.Expression<Func<OutboxMessage, bool>> which)
	{
		await using var scope = factory.Services.CreateAsyncScope();
		return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboxMessage>().AsNoTracking().SingleOrDefaultAsync(which, Ct);
	}
}
#endif
