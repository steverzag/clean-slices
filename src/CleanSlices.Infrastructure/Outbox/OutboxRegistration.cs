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
			services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.Section);
			services.TryAddScoped<IIntegrationEventPublisher, OutboxPublisher>();
			services.TryAddSingleton<OutboxProcessor>();
			services.AddHostedService<OutboxWorker>();

			return services;
		}
	}
}
#endif
