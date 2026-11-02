using CleanSlices.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanSlices.Infrastructure.Persistence.Configuration;

internal class UserTypeConfiguration : IEntityTypeConfiguration<User>
{
	public void Configure(EntityTypeBuilder<User> builder)
	{
		builder.Property(u => u.Name).HasMaxLength(User.NameMaxLength);
		builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength);

		builder.HasIndex(u => u.Email).IsUnique();
	}
}
