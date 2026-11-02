using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Widgets;

internal static class WidgetErrors
{
	internal static readonly ProblemError EmptyName = new("Widget.Name", "Name cannot be empty.");
	internal static readonly ProblemError NameTooLong = new("Widget.Name", $"Name cannot exceed {Widget.NameMaxLength} characters.");
}
