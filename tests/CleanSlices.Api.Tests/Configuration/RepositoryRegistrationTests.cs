#if (repository)
using CleanSlices.Api.Tests.Infrastructure;
using CleanSlices.Core.Domain;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Api.Tests.Configuration;

public class RepositoryRegistrationTests(ApiFactory factory)
{
	[Fact]
	public void Every_repository_interface_in_Core_has_an_implementation()
	{
		using var scope = factory.Services.CreateScope();

		var missing = typeof(IUnitOfWork).Assembly.GetTypes()
			.Where(t => t.IsInterface && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRepository<>)))
			.Where(t => scope.ServiceProvider.GetService(t) is null)
			.Select(t => t.Name);

		Assert.Empty(missing);
	}

	[Fact]
	public void The_unit_of_work_saves_the_context_the_repositories_use()
	{
		using var scope = factory.Services.CreateScope();

		Assert.Same(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
	}
}
#endif
