#if (outbox)
using System.Diagnostics;
using System.Text.Json;
using CleanSlices.Infrastructure.IntegrationEvents;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanSlices.Infrastructure.Outbox;

internal sealed class OutboxProcessor(
	IServiceScopeFactory scopes,
	IntegrationEventSerializer serializer,
	IMessageDispatcher dispatcher,
	IOptions<OutboxOptions> options,
	IHostEnvironment environment,
	ILogger<OutboxProcessor> logger) : IDisposable
{
	private const int LastErrorMaxLength = 4000;

	// The app's own source name is already traced by ServiceDefaults.
	private readonly ActivitySource _activities = new(environment.ApplicationName);

	// Returns how many messages were claimed: handled, retried or failed.
	public async Task<int> ProcessBatchAsync(CancellationToken ct)
	{
		var settings = options.Value;
		await using var scope = scopes.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var messages = context.Set<OutboxMessage>();
		var now = DateTimeOffset.UtcNow;
		var claim = Guid.NewGuid().ToString("N");

		// Claimed in one statement on every provider, so two instances never take the same row.
		var claimed = await messages
			.Where(m => m.ProcessedAt == null && m.FailedAt == null
				&& (m.LockedUntil == null || m.LockedUntil < now)
				&& (m.NextAttemptAt == null || m.NextAttemptAt <= now))
			.OrderBy(m => m.CreatedAt)
			.Take(settings.BatchSize)
			.ExecuteUpdateAsync(set => set
				.SetProperty(m => m.LockedUntil, now + settings.LeaseDuration)
				.SetProperty(m => m.LockedBy, claim), ct);

		if (claimed == 0)
		{
			return 0;
		}

		var batch = await messages.Where(m => m.LockedBy == claim).OrderBy(m => m.CreatedAt).ToListAsync(ct);
		foreach (var message in batch)
		{
			await HandleAsync(message, settings, ct);
			message.LockedUntil = null;
			message.LockedBy = null;
			await context.SaveChangesAsync(ct);
		}

		return claimed;
	}

	public Task<int> DeleteProcessedAsync(CancellationToken ct) =>
		DeleteProcessedAsync(DateTimeOffset.UtcNow - options.Value.Retention, ct);

	private async Task<int> DeleteProcessedAsync(DateTimeOffset before, CancellationToken ct)
	{
		await using var scope = scopes.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		return await context.Set<OutboxMessage>().Where(m => m.ProcessedAt < before).ExecuteDeleteAsync(ct);
	}

	private async Task HandleAsync(OutboxMessage message, OutboxOptions settings, CancellationToken ct)
	{
		using var activity = _activities.StartActivity($"outbox {message.Name}", ActivityKind.Consumer, message.TraceParent);
		activity?.SetTag("messaging.message.id", message.Id);
		message.Attempts++;

		IIntegrationEvent integrationEvent;
		try
		{
			integrationEvent = serializer.Deserialize(message.Name, message.Payload);
		}
		catch (Exception ex) when (ex is UnknownIntegrationEventException or JsonException)
		{
			// Retrying can't fix a name nobody knows or a payload that can't be read.
			Fail(message, ex, activity);
			return;
		}

		try
		{
			await dispatcher.Dispatch(new IntegrationEventMessage(message.Id, integrationEvent), ct);
			message.ProcessedAt = DateTimeOffset.UtcNow;
			message.LastError = null;
		}
		catch (Exception ex) when (!ct.IsCancellationRequested)
		{
			if (message.Attempts >= settings.MaxAttempts)
			{
				Fail(message, ex, activity);
				return;
			}

			var delay = settings.RetryDelay * Math.Pow(2, message.Attempts - 1);
			message.NextAttemptAt = DateTimeOffset.UtcNow + delay;
			message.LastError = Truncate(ex);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			logger.LogWarning(ex, "Integration event {Name} {Id} failed (attempt {Attempt}); retrying in {Delay}",
				message.Name, message.Id, message.Attempts, delay);
		}
	}

	private void Fail(OutboxMessage message, Exception ex, Activity? activity)
	{
		message.FailedAt = DateTimeOffset.UtcNow;
		message.LastError = Truncate(ex);
		activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
		logger.LogError(ex, "Integration event {Name} {Id} failed after {Attempts} attempt(s) and won't be retried",
			message.Name, message.Id, message.Attempts);
	}

	private static string Truncate(Exception ex)
	{
		var text = ex is AggregateException aggregate
			? string.Join(Environment.NewLine, aggregate.Flatten().InnerExceptions.Select(e => e.ToString()))
			: ex.ToString();
		return text.Length <= LastErrorMaxLength ? text : text[..LastErrorMaxLength];
	}

	public void Dispose() => _activities.Dispose();
}

internal sealed class OutboxWorker(OutboxProcessor processor, IOptions<OutboxOptions> options, ILogger<OutboxWorker> logger) : BackgroundService
{
	private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var nextCleanup = DateTimeOffset.UtcNow;
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				if (DateTimeOffset.UtcNow >= nextCleanup)
				{
					await processor.DeleteProcessedAsync(stoppingToken);
					nextCleanup = DateTimeOffset.UtcNow + CleanupInterval;
				}

				if (await processor.ProcessBatchAsync(stoppingToken) > 0)
				{
					continue;
				}
			}
			catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
			{
				// For example the database is down, or the OutboxMessages table has no migration yet.
				logger.LogError(ex, "The outbox processor failed; it tries again in {Interval}", options.Value.PollInterval);
			}

			try
			{
				await Task.Delay(options.Value.PollInterval, stoppingToken);
			}
			catch (OperationCanceledException)
			{
				return;
			}
		}
	}
}
#endif
