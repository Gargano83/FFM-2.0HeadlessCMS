using Microsoft.AspNetCore.Identity;

namespace MyCms.Data.Identity;

public class CmsRole : IdentityRole<Guid>
{
    public CmsRole() { }
    public CmsRole(string roleName) : base(roleName) { }
}