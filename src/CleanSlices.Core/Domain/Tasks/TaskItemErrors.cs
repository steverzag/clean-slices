using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Tasks;

internal class TaskItemErrors
{
	internal static readonly FieldError EmptyTitle = new(nameof(TaskItem.Title), "Title cannot be empty.");
	internal static readonly FieldError TitleTooLong = new(nameof(TaskItem.Title), $"Title cannot exceed {TaskItem.TitleMaxLength} characters.");
	internal static readonly FieldError EmptyDescription = new(nameof(TaskItem.Description), "Description cannot be empty.");
	internal static readonly FieldError DescriptionTooLong = new(nameof(TaskItem.Description), $"Description cannot exceed {TaskItem.DescriptionMaxLength} characters.");
}
