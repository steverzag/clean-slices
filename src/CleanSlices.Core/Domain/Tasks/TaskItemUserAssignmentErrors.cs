using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Tasks;

internal class TaskItemUserAssignmentErrors
{
	internal static readonly DomainError EmptyTaskItemId = new("TaskItemUserAssignment.TaskItemId", "Task item ID cannot be empty.");
	internal static readonly DomainError EmptyUserId = new("TaskItemUserAssignment.UserId", "User ID cannot be empty.");
}
