#if (inbox)
using CleanSlices.Infrastructure.IntegrationEvents;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Infrastructure.Inbox;

// One transaction around the handler: its saves and "this handler is done" commit together, or not at all.
internal sealed class InboxFilter : IIntegrationEventHandlerFilter
{
	public async Task Invoke(HandlerInvocation invocation, Func<Task> next, CancellationToken ct)
	{
		var context = invocation.Services.GetRequiredService<AppDbContext>();
		var (messageId, handler) = (invocation.Message.Id, invocation.Handler);

		if (await IsProcessedAsync(context, messageId, handler, ct))
		{
			return;
		}

		try
		{
			// The retrying execution strategy only accepts a transaction it can run again as a whole.
			await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
			{
				context.ChangeTracker.Clear();
				await using var transaction = await context.Database.BeginTransactionAsync(ct);
				context.Set<ProcessedMessage>().Add(new ProcessedMessage { MessageId = messageId, Handler = handler });
				await next();
				await context.SaveChangesAsync(ct);
				await transaction.CommitAsync(ct);
			});
		}
		catch (DbUpdateException)
		{
			// Another instance handled the same message at the same time (an expired lease) and saved first.
			if (!await IsProcessedAfterFailureAsync(context, messageId, handler, ct))
			{
				throw;
			}
		}
	}

	// Asking again instead of reading the provider's error number keeps this provider-neutral.
	private static async Task<bool> IsProcessedAfterFailureAsync(AppDbContext context, Guid messageId, string handler, CancellationToken ct)
	{
		context.ChangeTracker.Clear();
		return await IsProcessedAsync(context, messageId, handler, ct);
	}

	private static Task<bool> IsProcessedAsync(AppDbContext context, Guid messageId, string handler, CancellationToken ct) =>
		context.Set<ProcessedMessage>().AnyAsync(m => m.MessageId == messageId && m.Handler == handler, ct);
}
#endif
