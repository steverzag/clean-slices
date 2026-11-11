#if (inbox)
using CleanSlices.Api.Tests.Infrastructure;
using CleanSlices.Api.Tests.IntegrationEvents;
using CleanSlices.Infrastructure.Inbox;
using CleanSlices.Infrastructure.IntegrationEvents;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Api.Tests.Inbox;

public class InboxTests(ApiFactory factory)
{
	private CancellationToken Ct => TestContext.Current.CancellationToken;

	// The handlers directly: the inbox works the same whatever delivered the message.
	private IntegrationEventHandlers Handlers => factory.Services.GetRequiredService<IntegrationEventHandlers>();

	[Fact]
	public async Task A_message_delivered_twice_is_handled_once()
	{
		var message = new IntegrationEventMessage(Guid.NewGuid(), new TestEvent(Guid.NewGuid()));

		await Handlers.RunAsync(message, Ct);
		await Handlers.RunAsync(message, Ct);

		var testId = ((TestEvent)message.Event).TestId;
		Assert.Equal(1, RecordingHandler.DeliveriesOf(testId));
		Assert.Equal([typeof(FailingHandler).FullName!, typeof(RecordingHandler).FullName!], await HandlersRecordedForAsync(message.Id));
	}

	[Fact]
	public async Task A_redelivery_skips_the_handlers_that_succeeded_and_retries_the_one_that_failed()
	{
		var message = new IntegrationEventMessage(Guid.NewGuid(), new TestEvent(Guid.NewGuid(), Fail: true));

		await Assert.ThrowsAsync<AggregateException>(() => Handlers.RunAsync(message, Ct));
		await Assert.ThrowsAsync<AggregateException>(() => Handlers.RunAsync(message, Ct));

		Assert.Equal(1, RecordingHandler.DeliveriesOf(((TestEvent)message.Event).TestId));
		Assert.Equal([typeof(RecordingHandler).FullName!], await HandlersRecordedForAsync(message.Id));
	}

	[Fact]
	public async Task The_same_message_handled_twice_at_once_is_recorded_once_without_failing()
	{
		var message = new IntegrationEventMessage(Guid.NewGuid(), new TestEvent(Guid.NewGuid()));

		await Task.WhenAll(Handlers.RunAsync(message, Ct), Handlers.RunAsync(message, Ct));

		Assert.Equal(2, (await HandlersRecordedForAsync(message.Id)).Count);
	}

	[Fact]
	public async Task A_handler_that_fails_after_its_own_save_runs_again_on_redelivery()
	{
		var testId = Guid.NewGuid();
		var message = new IntegrationEventMessage(Guid.NewGuid(), new SavingEvent(testId, FailAfterSaving: true));

		await Assert.ThrowsAsync<AggregateException>(() => Handlers.RunAsync(message, Ct));
		await Assert.ThrowsAsync<AggregateException>(() => Handlers.RunAsync(message, Ct));

		Assert.Equal(2, SavingHandler.RunsOf(testId));
		Assert.Empty(await HandlersRecordedForAsync(message.Id));
	}

	[Fact]
	public async Task Two_deliveries_inside_a_saving_handler_at_once_are_recorded_once_without_failing()
	{
		var testId = Guid.NewGuid();
		var message = new IntegrationEventMessage(Guid.NewGuid(), new SavingEvent(testId));
		var gate = SavingHandler.HoldBeforeSaving(testId);

		var deliveries = Task.WhenAll(Handlers.RunAsync(message, Ct), Handlers.RunAsync(message, Ct));
		await SavingHandler.WaitForRunsAsync(testId, 2, Ct);
		gate.SetResult();
		await deliveries;

		Assert.Equal(2, SavingHandler.RunsOf(testId));
		Assert.Single(await HandlersRecordedForAsync(message.Id));
	}

	[Fact]
	public async Task Old_records_are_deleted_after_the_retention()
	{
		var message = new IntegrationEventMessage(Guid.NewGuid(), new TestEvent(Guid.NewGuid()));
		await Handlers.RunAsync(message, Ct);

		await using (var scope = factory.Services.CreateAsyncScope())
		{
			await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<ProcessedMessage>()
				.Where(m => m.MessageId == message.Id)
				.ExecuteUpdateAsync(set => set.SetProperty(m => m.ProcessedAt, DateTimeOffset.UtcNow.AddDays(-8)), Ct);
		}

		await factory.Services.GetRequiredService<InboxCleanup>().DeleteOldRecordsAsync(Ct);

		Assert.Empty(await HandlersRecordedForAsync(message.Id));
	}

	private async Task<List<string>> HandlersRecordedForAsync(Guid messageId)
	{
		await using var scope = factory.Services.CreateAsyncScope();
		return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<ProcessedMessage>()
			.Where(m => m.MessageId == messageId)
			.OrderBy(m => m.Handler)
			.Select(m => m.Handler)
			.ToListAsync(Ct);
	}
}
#endif
