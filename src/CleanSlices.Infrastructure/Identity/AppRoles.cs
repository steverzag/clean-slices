#if (identity)
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanSlices.Infrastructure.Identity;

public static class AppRoles
{
	public const string Admin = "admin";
}

// Roles are data: seeded here, so they come with the migration (or EnsureCreated).
internal sealed class AppRolesTypeConfiguration : IEntityTypeConfiguration<IdentityRole>
{
	public void Configure(EntityTypeBuilder<IdentityRole> builder) =>
		builder.HasData(new IdentityRole
		{
			Id = "0198c5a4-0000-7000-8000-000000000001",
			Name = AppRoles.Admin,
			NormalizedName = AppRoles.Admin.ToUpperInvariant(),
			ConcurrencyStamp = "0198c5a4-0000-7000-8000-000000000001",
		});
}
#endif
