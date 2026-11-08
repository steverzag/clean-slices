#if (identity)
using Microsoft.AspNetCore.Identity;

namespace CleanSlices.Infrastructure.Identity;

// A login account, not a domain entity. Add profile fields here.
public class AppUser : IdentityUser
{
}
#endif
