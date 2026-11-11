using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Tasks;

public class TaskItemUserAssignment
{
	public Guid TaskItemId { get; private set; }
	public Guid UserId { get; private set; }
	public DateTimeOffset AssignedOn { get; private set; }

	private TaskItemUserAssignment() { } // For EF Core

	private TaskItemUserAssignment(Guid taskItemId, Guid userId)
	{
		TaskItemId = taskItemId;
		UserId = userId;
		AssignedOn = DateTimeOffset.UtcNow;
	}

#if (result)
	public static Result<TaskItemUserAssignment> Create(Guid taskItemId, Guid userId)
	{
		var errors = new List<FieldError>();
		if (taskItemId == Guid.Empty)
		{
			errors.Add(TaskItemUserAssignmentErrors.EmptyTaskItemId);
		}
		if (userId == Guid.Empty)
		{
			errors.Add(TaskItemUserAssignmentErrors.EmptyUserId);
		}

		if(errors.TryGetValidationError(out var validationError))
		{
			return Result.Failure<TaskItemUserAssignment>(validationError);
		}

		var assignment = new TaskItemUserAssignment(taskItemId, userId);
		return Result.Success(assignment);
	}
#else
	public static TaskItemUserAssignment Create(Guid taskItemId, Guid userId)
	{
		DomainValidationException.ThrowIfAny(
			taskItemId == Guid.Empty ? TaskItemUserAssignmentErrors.EmptyTaskItemId : null,
			userId == Guid.Empty ? TaskItemUserAssignmentErrors.EmptyUserId : null);

		return new TaskItemUserAssignment(taskItemId, userId);
	}
#endif
}
