#if (inbox)
using CleanSlices.Infrastructure.IntegrationEvents;
using CleanSlices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Infrastructure.Inbox;

// The record joins the handler's own save, so the handler's changes and "this handler is done" commit together.
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

		context.Set<ProcessedMessage>().Add(new ProcessedMessage { MessageId = messageId, Handler = handler });
		await next();

		try
		{
			await context.SaveChangesAsync(ct);
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
