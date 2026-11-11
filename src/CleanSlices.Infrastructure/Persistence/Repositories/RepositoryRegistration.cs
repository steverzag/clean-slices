#if (repository)
using CleanSlices.Core.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleanSlices.Infrastructure.Persistence.Repositories;

internal static class RepositoryRegistration
{
	extension(IServiceCollection services)
	{
		// Each repository is registered as its entity's interface (IWidgetRepository), found by scanning.
		public IServiceCollection AddRepositories()
		{
			var registrations = typeof(RepositoryRegistration).Assembly.GetTypes()
				.Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
				.SelectMany(t => t.GetInterfaces()
					.Where(i => i.GetInterfaces().Any(b => b.IsGenericType && b.GetGenericTypeDefinition() == typeof(IRepository<>)))
					.Select(i => ServiceDescriptor.Scoped(i, t)))
				.ToList();

			if (registrations.GroupBy(d => d.ServiceType).FirstOrDefault(g => g.Count() > 1) is { } duplicate)
			{
				throw new InvalidOperationException(
					$"{duplicate.Key} has more than one implementation: {string.Join(", ", duplicate.Select(d => d.ImplementationType!.FullName))}.");
			}

			services.TryAdd(registrations);
			services.TryAddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

			return services;
		}
	}
}
#endif
