#if (cqrs)
using CleanSlices.Core.Domain;
#endif
using CleanSlices.Core.Messages;
#if (current_user)
using CleanSlices.Infrastructure.CurrentUser;
#endif
#if (inbox)
using CleanSlices.Infrastructure.Inbox;
#endif
#if (integration_events)
using CleanSlices.Infrastructure.IntegrationEvents;
#endif
using CleanSlices.Infrastructure.Messages;
#if (outbox)
using CleanSlices.Infrastructure.Outbox;
#endif
using CleanSlices.Infrastructure.Persistence;
#if (repository)
using CleanSlices.Infrastructure.Persistence.Repositories;
#endif
using CleanSlices.Infrastructure.Persistence.SaveChanges;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CleanSlices.Infrastructure;

public static class InfrastructureRegistration
{
	public const string DatabaseConnectionName = "Database";

	extension(IHostApplicationBuilder builder)
	{
		public IHostApplicationBuilder AddInfrastructure()
		{
			var services = builder.Services;

			services.TryAddScoped<IPublisher, Publisher>();
			services.AddNotificationHandlers();
			services.AddSaveChangesSteps();
			services.TryAddScoped<SaveChangesPipeline>();
#if (repository)
			services.AddRepositories();
#endif
#if (cqrs)
			services.TryAddScoped<IReadDb>(sp => sp.GetRequiredService<AppDbContext>());
#endif
#if (current_user)
			services.TryAddSingleton<ICurrentUser, AnonymousCurrentUser>();
#endif
#if (cache)
			// In memory, for this instance only; the Redis option registers a shared one.
			services.AddDistributedMemoryCache();
#endif
#if (integration_events)
			services.AddIntegrationEvents();
#endif
#if (outbox)
			services.AddOutbox();
#endif
#if (inbox)
			services.AddInbox();
#endif

#if (postgresql)
			services.AddDbContext<AppDbContext>((sp, o) =>
				o.UseNpgsql(builder.Configuration.GetConnectionString(DatabaseConnectionName))
					.UseSnakeCaseNamingConvention()
					.AddInterceptors(sp.GetRequiredService<SaveChangesPipeline>()));

			builder.EnrichNpgsqlDbContext<AppDbContext>();
#else
			services.AddDbContext<AppDbContext>((sp, o) =>
				o.UseSqlServer(builder.Configuration.GetConnectionString(DatabaseConnectionName))
					.AddInterceptors(sp.GetRequiredService<SaveChangesPipeline>()));

			builder.EnrichSqlServerDbContext<AppDbContext>();
#endif

			return builder;
		}
	}

	extension(IServiceCollection services)
	{
		private void AddNotificationHandlers()
		{
			var handlerInterface = typeof(INotificationHandler<>);

			var registrations = typeof(InfrastructureRegistration).Assembly.GetTypes()
				.Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
				.SelectMany(t => t.GetInterfaces()
					.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
					.Select(i => ServiceDescriptor.Scoped(i, t)));

			services.TryAddEnumerable(registrations);
		}

		private void AddSaveChangesSteps()
		{
			var registrations = typeof(InfrastructureRegistration).Assembly.GetTypes()
				.Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
					&& typeof(ISaveChangesStep).IsAssignableFrom(t))
				.Select(t => ServiceDescriptor.Scoped(typeof(ISaveChangesStep), t));

			services.TryAddEnumerable(registrations);
		}
	}
}
