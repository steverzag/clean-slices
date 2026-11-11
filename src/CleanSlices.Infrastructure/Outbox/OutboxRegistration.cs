#if (outbox)
using CleanSlices.Infrastructure.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleanSlices.Infrastructure.Outbox;

internal static class OutboxRegistration
{
	extension(IServiceCollection services)
	{
		public IServiceCollection AddOutbox()
		{
			services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.Section)
				.Validate(o => o.PollInterval > TimeSpan.Zero && o.LeaseDuration > TimeSpan.Zero && o.Retention > TimeSpan.Zero && o.RetryDelay >= TimeSpan.Zero,
					"Outbox: PollInterval, LeaseDuration and Retention must be positive, and RetryDelay can't be negative.")
				.Validate(o => o is { BatchSize: > 0, MaxAttempts: > 0 }, "Outbox: BatchSize and MaxAttempts must be at least 1.")
				.ValidateOnStart();
			services.TryAddScoped<IIntegrationEventPublisher, OutboxPublisher>();
			services.TryAddSingleton<OutboxProcessor>();
			services.AddHostedService<OutboxWorker>();

			return services;
		}
	}
}
#endif
