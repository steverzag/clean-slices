#if (inbox)
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleanSlices.Infrastructure.Inbox;

internal static class InboxRegistration
{
	extension(IServiceCollection services)
	{
		// InboxFilter itself is found by AddIntegrationEvents, like every handler filter.
		public IServiceCollection AddInbox()
		{
			services.AddOptions<InboxOptions>().BindConfiguration(InboxOptions.Section)
				.Validate(o => o.Retention > TimeSpan.Zero, "Inbox: Retention must be positive.")
				.ValidateOnStart();
			services.TryAddSingleton<InboxCleanup>();
			services.AddHostedService<InboxCleanupWorker>();

			return services;
		}
	}
}
#endif
