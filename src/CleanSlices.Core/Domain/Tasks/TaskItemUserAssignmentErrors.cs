using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Tasks;

internal class TaskItemUserAssignmentErrors
{
	internal static readonly FieldError EmptyTaskItemId = new(nameof(TaskItemUserAssignment.TaskItemId), "Task item ID cannot be empty.");
	internal static readonly FieldError EmptyUserId = new(nameof(TaskItemUserAssignment.UserId), "User ID cannot be empty.");
}
