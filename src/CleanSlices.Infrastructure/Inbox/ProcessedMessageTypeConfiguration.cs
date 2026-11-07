#if (inbox)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanSlices.Infrastructure.Inbox;

internal sealed class ProcessedMessageTypeConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
	public const int HandlerMaxLength = 400;

	public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
	{
		builder.ToTable("ProcessedMessages");

		// One record per message and handler: a redelivery skips the handlers that already succeeded.
		builder.HasKey(m => new { m.MessageId, m.Handler });
		builder.Property(m => m.Handler).HasMaxLength(HandlerMaxLength);
		builder.HasIndex(m => m.ProcessedAt);
	}
}
#endif
