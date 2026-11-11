#if (integration_events)
using System.Collections.Concurrent;
using CleanSlices.Infrastructure.IntegrationEvents;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Api.Tests.IntegrationEvents;

// Tests share one host, so the handlers only record or fail for the test id they're given.
public sealed record TestEvent(Guid TestId, bool Fail = false) : IIntegrationEvent;

public sealed class RecordingHandler : IIntegrationEventHandler<TestEvent>
{
	private static readonly ConcurrentDictionary<Guid, int> Deliveries = [];

	public static int DeliveriesOf(Guid testId) => Deliveries.GetValueOrDefault(testId);

	// Delivery through a broker happens in the background: wait for it, up to a deadline.
	public static async Task<int> WaitForDeliveriesAsync(Guid testId, int expected, CancellationToken ct)
	{
		var deadline = DateTime.UtcNow.AddSeconds(30);
		while (DeliveriesOf(testId) < expected && DateTime.UtcNow < deadline)
		{
			await Task.Delay(50, ct);
		}

		return DeliveriesOf(testId);
	}

	public Task Handle(TestEvent integrationEvent, CancellationToken ct)
	{
		Deliveries.AddOrUpdate(integrationEvent.TestId, 1, (_, count) => count + 1);
		return Task.CompletedTask;
	}
}

public sealed class FailingHandler : IIntegrationEventHandler<TestEvent>
{
	public Task Handle(TestEvent integrationEvent, CancellationToken ct) =>
		integrationEvent.Fail
			? throw new InvalidOperationException($"Failing on purpose for {integrationEvent.TestId}.")
			: Task.CompletedTask;
}

public sealed record SavingEvent(Guid TestId, bool FailAfterSaving = false) : IIntegrationEvent;

// Saves by itself, the way a handler with an external call after its save does.
public sealed class SavingHandler(AppDbContext context) : IIntegrationEventHandler<SavingEvent>
{
	private static readonly ConcurrentDictionary<Guid, int> Runs = [];
	private static readonly ConcurrentDictionary<Guid, TaskCompletionSource> Gates = [];

	public static int RunsOf(Guid testId) => Runs.GetValueOrDefault(testId);

	public static TaskCompletionSource HoldBeforeSaving(Guid testId) =>
		Gates.GetOrAdd(testId, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

	public static async Task WaitForRunsAsync(Guid testId, int expected, CancellationToken ct)
	{
		var deadline = DateTime.UtcNow.AddSeconds(30);
		while (RunsOf(testId) < expected && DateTime.UtcNow < deadline)
		{
			await Task.Delay(50, ct);
		}
	}

	public async Task Handle(SavingEvent integrationEvent, CancellationToken ct)
	{
		Runs.AddOrUpdate(integrationEvent.TestId, 1, (_, count) => count + 1);
		if (Gates.TryGetValue(integrationEvent.TestId, out var gate))
		{
			await gate.Task.WaitAsync(ct);
		}

		await context.SaveChangesAsync(ct);
		if (integrationEvent.FailAfterSaving)
		{
			throw new InvalidOperationException($"Failing on purpose for {integrationEvent.TestId}.");
		}
	}
}

// System.Text.Json can't create it: no constructor it may use.
public sealed class UnreadableEvent : IIntegrationEvent
{
	private UnreadableEvent()
	{
	}
}

public sealed class UnreadableHandler : IIntegrationEventHandler<UnreadableEvent>
{
	public Task Handle(UnreadableEvent integrationEvent, CancellationToken ct) => Task.CompletedTask;
}

public static class TestEventHandlers
{
	public static void Add(IServiceCollection services)
	{
		services.AddScoped<IIntegrationEventHandler<TestEvent>, RecordingHandler>();
		services.AddScoped<IIntegrationEventHandler<TestEvent>, FailingHandler>();
		services.AddScoped<IIntegrationEventHandler<SavingEvent>, SavingHandler>();
		services.AddScoped<IIntegrationEventHandler<UnreadableEvent>, UnreadableHandler>();
	}
}
#endif
