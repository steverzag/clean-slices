#if (signalr)
#if (signalr_redis)
using CleanSlices.Infrastructure;
#endif
using CleanSlices.Infrastructure.Realtime;
#if (signalr_redis)
using StackExchange.Redis;
#endif

namespace CleanSlices.Api.Configuration.Realtime;

public static class RealtimeRegistration
{
	public const string HubsPath = "/hubs";
	public const string NotificationsHubPath = $"{HubsPath}/notifications";

	extension(IHostApplicationBuilder builder)
	{
		public IHostApplicationBuilder AddRealtime()
		{
#if (signalr_redis)
			// Every instance publishes to Redis, so a client connected to any instance gets the message.
			builder.Services.AddSignalR().AddStackExchangeRedis(
				builder.Configuration.GetConnectionString(InfrastructureRegistration.CacheConnectionName)!,
				options => options.Configuration.ChannelPrefix = RedisChannel.Literal("cleanslices"));
#else
			builder.Services.AddSignalR();
#endif
			builder.Services.AddScoped<IRealtimeNotifier, SignalRNotifier>();

			return builder;
		}
	}

	extension(WebApplication app)
	{
#if (authentication)
		// Browsers can't send headers on a WebSocket, so SignalR sends the token as ?access_token=.
		// Only hub requests are read this way: a token in a URL can end up in logs.
		public WebApplication UseHubAccessTokens()
		{
			app.Use((context, next) =>
			{
				if (context.Request.Path.StartsWithSegments(HubsPath)
					&& !context.Request.Headers.ContainsKey("Authorization")
					&& context.Request.Query.TryGetValue("access_token", out var token))
				{
					context.Request.Headers.Authorization = $"Bearer {token}";
				}

				return next(context);
			});

			return app;
		}

#endif
		public WebApplication MapRealtime()
		{
			app.MapHub<NotificationsHub>(NotificationsHubPath);
			return app;
		}
	}
}
#endif
