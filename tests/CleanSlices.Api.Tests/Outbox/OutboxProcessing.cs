#if (outbox)
using System.Linq.Expressions;
using CleanSlices.Api.Tests.Infrastructure;
using CleanSlices.Infrastructure.Outbox;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Api.Tests.Outbox;

internal static class OutboxProcessing
{
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(50);

	// Other tests process the same table at the same time, and one of them may hold this row in its batch:
	// wait for the row's state until a deadline, not for a number of rounds.
	public static async Task<OutboxMessage> ProcessOutboxUntilAsync(
		this ApiFactory factory,
		Expression<Func<OutboxMessage, bool>> which,
		Func<OutboxMessage, bool> done,
		CancellationToken ct)
	{
		var processor = factory.Services.GetRequiredService<OutboxProcessor>();
		var deadline = DateTimeOffset.UtcNow + Timeout;
		while (true)
		{
			var claimed = await processor.ProcessBatchAsync(ct);
			var message = await factory.FindOutboxMessageAsync(which, ct);
			if (message is not null && done(message))
			{
				return message;
			}

			if (DateTimeOffset.UtcNow > deadline)
			{
				throw new TimeoutException(message is null
					? "The outbox row was never written."
					: $"The outbox row {message.Id} ({message.Name}) didn't reach the expected state: attempts {message.Attempts}, " +
						$"processed {message.ProcessedAt?.ToString("O") ?? "no"}, failed {message.FailedAt?.ToString("O") ?? "no"}, " +
						$"locked until {message.LockedUntil?.ToString("O") ?? "-"}, last error {message.LastError ?? "-"}.");
			}

			if (claimed == 0)
			{
				await Task.Delay(Pause, ct);
			}
		}
	}

	public static async Task<OutboxMessage?> FindOutboxMessageAsync(
		this ApiFactory factory,
		Expression<Func<OutboxMessage, bool>> which,
		CancellationToken ct)
	{
		await using var scope = factory.Services.CreateAsyncScope();
		return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboxMessage>().AsNoTracking().SingleOrDefaultAsync(which, ct);
	}
}
#endif
