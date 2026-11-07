#if (outbox)
using System.Collections.Concurrent;
using CleanSlices.Infrastructure.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Api.Tests.Outbox;

// Tests share one host, so the handlers only record or fail for the test id they're given.
public sealed record OutboxTestEvent(Guid TestId, bool Fail = false) : IIntegrationEvent;

public sealed class RecordingHandler : IIntegrationEventHandler<OutboxTestEvent>
{
	private static readonly ConcurrentDictionary<Guid, int> Deliveries = [];

	public static int DeliveriesOf(Guid testId) => Deliveries.GetValueOrDefault(testId);

	public Task Handle(OutboxTestEvent integrationEvent, CancellationToken ct)
	{
		Deliveries.AddOrUpdate(integrationEvent.TestId, 1, (_, count) => count + 1);
		return Task.CompletedTask;
	}
}

public sealed class FailingHandler : IIntegrationEventHandler<OutboxTestEvent>
{
	public Task Handle(OutboxTestEvent integrationEvent, CancellationToken ct) =>
		integrationEvent.Fail
			? throw new InvalidOperationException($"Failing on purpose for {integrationEvent.TestId}.")
			: Task.CompletedTask;
}

public static class OutboxTestServices
{
	public static void Add(IServiceCollection services)
	{
		services.AddScoped<IIntegrationEventHandler<OutboxTestEvent>, RecordingHandler>();
		services.AddScoped<IIntegrationEventHandler<OutboxTestEvent>, FailingHandler>();
	}
}
#endif
