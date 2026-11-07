#if (inbox)
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanSlices.Infrastructure.Inbox;

internal sealed class InboxCleanup(IServiceScopeFactory scopes, IOptions<InboxOptions> options)
{
	public async Task<int> DeleteOldRecordsAsync(CancellationToken ct)
	{
		var before = DateTimeOffset.UtcNow - options.Value.Retention;
		await using var scope = scopes.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		return await context.Set<ProcessedMessage>().Where(m => m.ProcessedAt < before).ExecuteDeleteAsync(ct);
	}
}

internal sealed class InboxCleanupWorker(InboxCleanup cleanup, ILogger<InboxCleanupWorker> logger) : BackgroundService
{
	private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await cleanup.DeleteOldRecordsAsync(stoppingToken);
			}
			catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
			{
				logger.LogError(ex, "Deleting old inbox records failed; it tries again in {Interval}", Interval);
			}

			try
			{
				await Task.Delay(Interval, stoppingToken);
			}
			catch (OperationCanceledException)
			{
				return;
			}
		}
	}
}
#endif
