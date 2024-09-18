using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Auth;

public class SecuritySettings
{
    public const string SECTION_NAME = "Security";

    public string JwtSigningKey { get; set; }

    public int JwtExpiryInMinutes { get; set; }

    public IList<DevUser> DevUsers { get; set; } = [];

    public class DevUser
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        public string[] Roles { get; set; } = [];
    }
}
