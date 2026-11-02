using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Tasks;

internal class TaskItemErrors
{
	internal static readonly ProblemError EmptyTitle = new("TaskItem.Title", "Title cannot be empty.");
	internal static readonly ProblemError TitleTooLong = new("TaskItem.Title", $"Title cannot exceed {TaskItem.TitleMaxLength} characters.");
	internal static readonly ProblemError EmptyDescription = new("TaskItem.Description", "Description cannot be empty.");
	internal static readonly ProblemError DescriptionTooLong = new("TaskItem.Description", $"Description cannot exceed {TaskItem.DescriptionMaxLength} characters.");
}
