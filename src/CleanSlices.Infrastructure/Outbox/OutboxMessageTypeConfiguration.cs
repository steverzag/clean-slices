#if (outbox)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanSlices.Infrastructure.Outbox;

internal sealed class OutboxMessageTypeConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
	public const int NameMaxLength = 200;
	public const int LockedByMaxLength = 64;

	public void Configure(EntityTypeBuilder<OutboxMessage> builder)
	{
		builder.ToTable("OutboxMessages");

		builder.Property(m => m.Name).HasMaxLength(NameMaxLength);
		builder.Property(m => m.LockedBy).HasMaxLength(LockedByMaxLength);

		// The processor looks for pending rows, oldest first.
		builder.HasIndex(m => new { m.ProcessedAt, m.FailedAt, m.CreatedAt });
	}
}
#endif
