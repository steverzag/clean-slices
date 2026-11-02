using CleanSlices.Core.Domain.Widgets;
using CleanSlices.Core.Messages;
using Microsoft.Extensions.Logging;

namespace CleanSlices.Infrastructure.EventHandlers.Widgets;

internal sealed class WidgetDeletedEventHandler(ILogger<WidgetDeletedEventHandler> logger) : INotificationHandler<WidgetDeletedDomainEvent>
{
	public Task Handle(WidgetDeletedDomainEvent notification, CancellationToken cancellationToken)
	{
		logger.LogInformation("Widget {WidgetId} deleted", notification.Id);
		return Task.CompletedTask;
	}
}
