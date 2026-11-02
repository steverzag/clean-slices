using CleanSlices.Core.Domain.Widgets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanSlices.Infrastructure.Persistence.Configuration;

internal class WidgetTypeConfiguration : IEntityTypeConfiguration<Widget>
{
	public void Configure(EntityTypeBuilder<Widget> builder)
	{
		// Explicit table name: features don't add a DbSet to AppDbContext, so EF would otherwise use the class name.
		builder.ToTable("Widgets");

		builder.Property(e => e.Name).HasMaxLength(Widget.NameMaxLength);
	}
}
