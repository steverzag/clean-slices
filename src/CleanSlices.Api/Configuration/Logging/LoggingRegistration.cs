#if (serilog)
using CleanSlices.Infrastructure.CurrentUser;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace CleanSlices.Api.Configuration.Logging;

public static class LoggingRegistration
{
#if (seq)
	public const string SeqConnectionName = "seq";

#endif
	extension(IHostApplicationBuilder builder)
	{
		// Code keeps using ILogger<T>. The defaults are here; a "Serilog" section in appsettings overrides them.
		public IHostApplicationBuilder AddSerilogLogging()
		{
			var configuration = builder.Configuration;
			var development = builder.Environment.IsDevelopment();

			builder.Services.AddSerilog((services, logger) =>
			{
				logger
					.MinimumLevel.Information()
					.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
					.MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
					.Enrich.FromLogContext()
					.ReadFrom.Configuration(configuration)
					.ReadFrom.Services(services);

				if (development)
				{
					logger.WriteTo.Console();
				}
				else
				{
					logger.WriteTo.Console(new RenderedCompactJsonFormatter());
				}

				// The AppHost sets the OTEL_* variables, which the sink reads: the logs reach the dashboard.
				if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
				{
					logger.WriteTo.OpenTelemetry();
				}
#if (seq)

				if (configuration.GetConnectionString(SeqConnectionName) is { Length: > 0 } seq)
				{
					logger.WriteTo.Seq(seq);
				}
#endif
			});

			return builder;
		}
	}

	extension(WebApplication app)
	{
		// One line per request: method, path, status and duration, with the trace and the user.
		// Written through this app's logger, not the static Log.Logger, which another host in the process may replace.
		public WebApplication UseRequestLogging()
		{
			app.UseSerilogRequestLogging(options =>
			{
				options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
				options.EnrichDiagnosticContext = (diagnostics, context) =>
					diagnostics.Set("UserId", context.RequestServices.GetRequiredService<ICurrentUser>().UserId);
			});

			return app;
		}
	}
}
#endif
