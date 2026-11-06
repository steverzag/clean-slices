using CleanSlices.Core.Shared;

namespace CleanSlices.Api.Features.Widgets;

internal static class WidgetErrors
{
#if (result)
	internal static NotFoundError NotFound(Guid id) => new("Widget.NotFound", $"Widget '{id}' was not found.");
#else
	internal static NotFoundException NotFound(Guid id) => new("Widget.NotFound", $"Widget '{id}' was not found.");
#endif
}
