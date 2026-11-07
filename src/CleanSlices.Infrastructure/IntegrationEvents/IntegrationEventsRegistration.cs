#if (integration_events)
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleanSlices.Infrastructure.IntegrationEvents;

internal static class IntegrationEventsRegistration
{
	extension(IServiceCollection services)
	{
		public IServiceCollection AddIntegrationEvents()
		{
			var assembly = typeof(IntegrationEventsRegistration).Assembly;
			var handlerInterface = typeof(IIntegrationEventHandler<>);

			services.TryAddEnumerable(assembly.GetTypes()
				.Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
				.SelectMany(t => t.GetInterfaces()
					.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
					.Select(i => ServiceDescriptor.Scoped(i, t))));

			services.TryAddEnumerable(assembly.GetTypes()
				.Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IIntegrationEventHandlerFilter).IsAssignableFrom(t))
				.Select(t => ServiceDescriptor.Scoped(typeof(IIntegrationEventHandlerFilter), t)));

			// Built when first used, from the final registrations: handlers registered later (tests) count too.
			services.TryAddSingleton(_ => new IntegrationEventTypes(
				IntegrationEventTypes.FoundIn(assembly).Concat(services
					.Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == handlerInterface)
					.Select(d => d.ServiceType.GetGenericArguments()[0]))));

			services.TryAddSingleton<IntegrationEventSerializer>();
			services.TryAddSingleton<IMessageDispatcher, InProcessMessageDispatcher>();

			return services;
		}
	}
}
#endif
