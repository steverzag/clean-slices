using CleanSlices.Core.Domain.Tasks;
using CleanSlices.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanSlices.Infrastructure.Persistence.Configuration;

internal class TaskItemUserAssignmentTypeConfiguration : IEntityTypeConfiguration<TaskItemUserAssignment>
{
	public void Configure(EntityTypeBuilder<TaskItemUserAssignment> builder)
	{
		builder.ToTable("TaskItemUserAssignments");

		builder.HasKey(a => new { a.TaskItemId, a.UserId });
		builder.HasIndex(a => a.UserId);
		builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
	}
}
