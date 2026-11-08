#if (servicebus)
using CleanSlices.Infrastructure.IntegrationEvents.Broker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CleanSlices.Infrastructure.IntegrationEvents.ServiceBus;

// The topic and the subscription aren't created here: the AppHost declares them (the emulator can't create
// them at run time), and in Azure they come with the namespace.
internal static class ServiceBusRegistration
{
	extension(IHostApplicationBuilder builder)
	{
		public IHostApplicationBuilder AddServiceBus(string connectionName)
		{
			builder.AddAzureServiceBusClient(connectionName);

			var services = builder.Services;
			services.AddOptions<BrokerOptions>().BindConfiguration(BrokerOptions.Section);
			services.Replace(ServiceDescriptor.Singleton<IMessageDispatcher, ServiceBusDispatcher>());
			services.AddHostedService<ServiceBusConsumer>();

			return builder;
		}
	}
}
#endif
