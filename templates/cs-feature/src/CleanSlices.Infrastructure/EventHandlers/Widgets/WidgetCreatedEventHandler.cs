using CleanSlices.Core.Domain.Widgets;
using CleanSlices.Core.Messages;
using Microsoft.Extensions.Logging;

namespace CleanSlices.Infrastructure.EventHandlers.Widgets;

internal sealed class WidgetCreatedEventHandler(ILogger<WidgetCreatedEventHandler> logger) : INotificationHandler<WidgetCreatedDomainEvent>
{
	public Task Handle(WidgetCreatedDomainEvent notification, CancellationToken cancellationToken)
	{
		logger.LogInformation("Widget {WidgetId} created", notification.Id);
		return Task.CompletedTask;
	}
}
