using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Shared;

namespace CleanSlices.Core.Tests.Domain.Tasks;

public class TaskItemTests
{
	[Fact]
	public void Create_with_valid_input_starts_not_started_and_raises_created()
	{
		var task = TaskItem.Create("Write tests", "Cover the domain");

		Assert.Equal(TaskItemStatus.NotStarted, task.Status);
		Assert.Null(task.CompletedOn);
		Assert.IsType<TaskItemCreatedDomainEvent>(Assert.Single(task.DomainEvents));
	}

	[Fact]
	public void Create_reports_every_invalid_field()
	{
		var exception = Assert.Throws<DomainValidationException>(() => TaskItem.Create(" ", ""));

		Assert.Contains(exception.Errors, error => error.Field == nameof(TaskItem.Title));
		Assert.Contains(exception.Errors, error => error.Field == nameof(TaskItem.Description));
	}

	[Fact]
	public void Complete_sets_the_date_and_raises_completed_once()
	{
		var task = TaskItem.Create("Write tests", "Cover the domain");
		task.ClearDomainEvents();

		task.Complete();
		task.Complete();

		Assert.Equal(TaskItemStatus.Completed, task.Status);
		Assert.NotNull(task.CompletedOn);
		Assert.IsType<TaskItemCompletedDomainEvent>(Assert.Single(task.DomainEvents));
	}

	[Fact]
	public void Moving_back_from_completed_clears_the_date()
	{
		var task = TaskItem.Create("Write tests", "Cover the domain");
		task.Complete();

		task.MakeInProgress();

		Assert.Equal(TaskItemStatus.InProgress, task.Status);
		Assert.Null(task.CompletedOn);
	}

	[Fact]
	public void AssignUser_adds_the_user_once()
	{
		var task = TaskItem.Create("Write tests", "Cover the domain");
		var userId = Guid.CreateVersion7();

		task.AssignUser(userId);
		task.AssignUser(userId);

		Assert.Equal(userId, Assert.Single(task.UserIds));
	}

	[Fact]
	public void UnassignUser_removes_only_that_user()
	{
		var task = TaskItem.Create("Write tests", "Cover the domain");
		var ana = Guid.CreateVersion7();
		var luis = Guid.CreateVersion7();
		task.ApplyUsers([ana, luis]);

		task.UnassignUser(ana);

		Assert.Equal(luis, Assert.Single(task.UserIds));
	}

	[Fact]
	public void ApplyUsers_with_an_empty_id_changes_nothing()
	{
		var task = TaskItem.Create("Write tests", "Cover the domain");
		var ana = Guid.CreateVersion7();
		task.AssignUser(ana);

		Assert.Throws<DomainValidationException>(() => task.ApplyUsers([ana, Guid.Empty]));

		Assert.Equal(ana, Assert.Single(task.UserIds));
	}
}
