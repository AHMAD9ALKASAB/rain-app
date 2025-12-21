using Microsoft.AspNetCore.Identity;
using Rain.Domain.Enums;

namespace Rain.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public UserType UserType { get; set; }
        public string? DisplayName { get; set; }
    }
}
