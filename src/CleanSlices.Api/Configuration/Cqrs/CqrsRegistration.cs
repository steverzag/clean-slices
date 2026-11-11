#if (cqrs)
#if (handlers)
using Microsoft.Extensions.DependencyInjection.Extensions;
#endif

namespace CleanSlices.Api.Configuration.Cqrs;

public static class CqrsRegistration
{
	extension(IServiceCollection services)
	{
#if (mediator)
		// Scoped, because handlers use the request's AppDbContext. The source generator reads these options at build time.
		public IServiceCollection AddCqrs() =>
			services.AddMediator((MediatorOptions options) => options.ServiceLifetime = ServiceLifetime.Scoped);
#else
		// Every command and query handler in the Api, found by scanning.
		public IServiceCollection AddCqrs()
		{
			Type[] handlerInterfaces = [typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];
			var registrations = typeof(CqrsRegistration).Assembly.GetTypes()
				.Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
				.SelectMany(t => t.GetInterfaces()
					.Where(i => i.IsGenericType && handlerInterfaces.Contains(i.GetGenericTypeDefinition()))
					.Select(i => ServiceDescriptor.Scoped(i, t)))
				.ToList();

			if (registrations.GroupBy(d => d.ServiceType).FirstOrDefault(g => g.Count() > 1) is { } duplicate)
			{
				throw new InvalidOperationException(
					$"{duplicate.Key} has more than one handler: {string.Join(", ", duplicate.Select(d => d.ImplementationType!.FullName))}.");
			}

			services.TryAdd(registrations);
			services.TryAddScoped<ISender, Sender>();

			return services;
		}
#endif
	}
}
#endif
