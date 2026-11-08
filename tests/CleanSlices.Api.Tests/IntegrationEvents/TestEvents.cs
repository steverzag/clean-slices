#if (integration_events)
using System.Collections.Concurrent;
using CleanSlices.Infrastructure.IntegrationEvents;
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

public static class TestEventHandlers
{
	public static void Add(IServiceCollection services)
	{
		services.AddScoped<IIntegrationEventHandler<TestEvent>, RecordingHandler>();
		services.AddScoped<IIntegrationEventHandler<TestEvent>, FailingHandler>();
	}
}
#endif
