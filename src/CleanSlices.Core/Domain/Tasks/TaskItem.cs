using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Domain.Tasks;

public enum TaskItemStatus
{
	NotStarted,
	InProgress,
	Completed
}

public class TaskItem : Entity
{
	public const int TitleMaxLength = 200;
	public const int DescriptionMaxLength = 2000;

	public string Title { get; private set; } = null!;
	public string Description { get; private set; } = null!;
	public TaskItemStatus Status { get; private set; }
	public DateTimeOffset? CompletedOn { get; private set; }
	private readonly List<TaskItemUserAssignment> _userAssignments = [];
	public IReadOnlyList<TaskItemUserAssignment> UserAssignments => [.. _userAssignments];
	public IReadOnlySet<Guid> UserIds => new HashSet<Guid>(_userAssignments.Select(user => user.UserId));

	private TaskItem() { } // For EF Core

	private TaskItem(string title, string description)
	{
		Title = title;
		Description = description;
	}

	public static TaskItem Create(string title, string description)
	{
		DomainValidationException.ThrowIfAny(ValidateTitle(title), ValidateDescription(description));

		var task = new TaskItem(title, description);
		task.Raise(new TaskItemCreatedDomainEvent(task.Id));
		return task;
	}

	public void UpdateTitle(string title)
	{
		DomainValidationException.ThrowIfAny(ValidateTitle(title));
		Title = title;
	}

	public void UpdateDescription(string description)
	{
		DomainValidationException.ThrowIfAny(ValidateDescription(description));
		Description = description;
	}

	public void MakeNotStarted()
	{
		CompletedOn = null;
		Status = TaskItemStatus.NotStarted;
	}

	public void MakeInProgress()
	{
		CompletedOn = null;
		Status = TaskItemStatus.InProgress;
	}

	public void Complete()
	{
		if (Status == TaskItemStatus.Completed)
		{
			return;
		}

		CompletedOn = DateTimeOffset.UtcNow;
		Status = TaskItemStatus.Completed;
		Raise(new TaskItemCompletedDomainEvent(Id));
	}

	public void Delete()
	{
		Raise(new TaskItemDeletedDomainEvent(Id));
	}

	public void AssignUser(Guid userId)
	{
		var userIds = new HashSet<Guid>(UserIds) { userId };
		ApplyUsers(userIds);
	}

	public void UnassignUser(Guid userId)
	{
		var userIds = new HashSet<Guid>(UserIds);
		userIds.Remove(userId);
		ApplyUsers(userIds);
	}

	public void ApplyUsers(IEnumerable<Guid> userIds)
	{
		var target = userIds.ToHashSet();
		var current = UserIds;

		if (target.SetEquals(current))
		{
			return;
		}

		// Build every new assignment first, so an invalid id leaves the current assignments untouched.
		var toAdd = target.Except(current)
			.Select(userId => TaskItemUserAssignment.Create(Id, userId))
			.ToList();

		_userAssignments.RemoveAll(assignment => !target.Contains(assignment.UserId));
		_userAssignments.AddRange(toAdd);
	}

	private static DomainError? ValidateTitle(string title) =>
		string.IsNullOrWhiteSpace(title) ? TaskItemErrors.EmptyTitle
		: title.Length > TitleMaxLength ? TaskItemErrors.TitleTooLong
		: null;

	private static DomainError? ValidateDescription(string description) =>
		string.IsNullOrWhiteSpace(description) ? TaskItemErrors.EmptyDescription
		: description.Length > DescriptionMaxLength ? TaskItemErrors.DescriptionTooLong
		: null;
}
