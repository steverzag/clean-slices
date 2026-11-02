using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Tasks;

internal class TaskItemErrors
{
	internal static readonly DomainError EmptyTitle = new("TaskItem.Title", "Title cannot be empty.");
	internal static readonly DomainError TitleTooLong = new("TaskItem.Title", $"Title cannot exceed {TaskItem.TitleMaxLength} characters.");
	internal static readonly DomainError EmptyDescription = new("TaskItem.Description", "Description cannot be empty.");
	internal static readonly DomainError DescriptionTooLong = new("TaskItem.Description", $"Description cannot exceed {TaskItem.DescriptionMaxLength} characters.");
}
