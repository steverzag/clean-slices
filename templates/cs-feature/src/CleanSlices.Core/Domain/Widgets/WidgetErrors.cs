using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Widgets;

internal static class WidgetErrors
{
	internal static readonly FieldError EmptyName = new(nameof(Widget.Name), "Name cannot be empty.");
	internal static readonly FieldError NameTooLong = new(nameof(Widget.Name), $"Name cannot exceed {Widget.NameMaxLength} characters.");
}
