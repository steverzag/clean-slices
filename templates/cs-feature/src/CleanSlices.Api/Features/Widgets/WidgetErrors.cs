using CleanSlices.Core.Shared;

namespace CleanSlices.Api.Features.Widgets;

internal static class WidgetErrors
{
	internal static NotFoundError NotFound(Guid id) => new("Widget.NotFound", $"Widget '{id}' was not found.");
}
