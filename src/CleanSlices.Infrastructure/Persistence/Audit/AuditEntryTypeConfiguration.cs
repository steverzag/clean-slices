#if (audit)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanSlices.Infrastructure.Persistence.Audit;

internal sealed class AuditEntryTypeConfiguration : IEntityTypeConfiguration<AuditEntry>
{
	public void Configure(EntityTypeBuilder<AuditEntry> builder)
	{
		builder.ToTable("AuditEntries");
		builder.Property(e => e.EntityType).HasMaxLength(AuditEntry.EntityTypeMaxLength);
		builder.Property(e => e.EntityId).HasMaxLength(AuditEntry.EntityIdMaxLength);
		builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(10);
		builder.Property(e => e.UserId).HasMaxLength(AuditEntry.UserIdMaxLength);
		builder.Property(e => e.TraceId).HasMaxLength(AuditEntry.TraceIdMaxLength);

		// An entity's history, in order: also what "created by" and "last modified by" read.
		builder.HasIndex(e => new { e.EntityType, e.EntityId, e.Timestamp });
	}
}
#endif
