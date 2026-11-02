using CleanSlices.Core.Shared;

namespace CleanSlices.Api.Features.Tasks;

internal static class TaskItemErrors
{
	internal static NotFoundError NotFound(Guid id) => new("TaskItem.NotFound", $"Task item '{id}' was not found.");

	internal static NotFoundError UserNotFound(Guid userId) => new("TaskItem.UserNotFound", $"User '{userId}' was not found.");
}
