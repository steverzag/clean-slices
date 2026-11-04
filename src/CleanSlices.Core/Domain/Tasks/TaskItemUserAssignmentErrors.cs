using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Tasks;

internal class TaskItemUserAssignmentErrors
{
	internal static readonly FieldError EmptyTaskItemId = new("TaskItemUserAssignment.TaskItemId", "Task item ID cannot be empty.");
	internal static readonly FieldError EmptyUserId = new("TaskItemUserAssignment.UserId", "User ID cannot be empty.");
}
