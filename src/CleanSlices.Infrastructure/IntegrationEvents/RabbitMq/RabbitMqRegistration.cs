#if (rabbitmq)
using CleanSlices.Infrastructure.IntegrationEvents.Broker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CleanSlices.Infrastructure.IntegrationEvents.RabbitMq;

internal static class RabbitMqRegistration
{
	extension(IHostApplicationBuilder builder)
	{
		public IHostApplicationBuilder AddRabbitMq(string connectionName)
		{
			builder.AddRabbitMQClient(connectionName);

			var services = builder.Services;
			services.AddOptions<BrokerOptions>().BindConfiguration(BrokerOptions.Section);
			services.TryAddSingleton<RabbitMqTopology>();
			services.Replace(ServiceDescriptor.Singleton<IMessageDispatcher, RabbitMqDispatcher>());
			services.AddHostedService<RabbitMqConsumer>();

			return builder;
		}
	}
}
#endif
