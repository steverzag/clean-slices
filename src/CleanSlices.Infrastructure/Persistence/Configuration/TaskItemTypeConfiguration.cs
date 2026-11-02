using CleanSlices.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanSlices.Infrastructure.Persistence.Configuration;

internal class TaskItemTypeConfiguration : IEntityTypeConfiguration<TaskItem>
{
	public void Configure(EntityTypeBuilder<TaskItem> builder)
	{
		builder.Property(t => t.Title).HasMaxLength(TaskItem.TitleMaxLength);
		builder.Property(t => t.Description).HasMaxLength(TaskItem.DescriptionMaxLength);

		builder.HasMany(t => t.UserAssignments)
			.WithOne()
			.HasForeignKey(a => a.TaskItemId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
