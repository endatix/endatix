using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Identity.Seed
{
    /// <summary>
    /// Provides functionality for seeding initial identity data in the application
    /// </summary>
    public static class IdentitySeed
    {
        private const string DEFAULT_ADMIN_EMAIL = "admin@endatix.com";
        private const string DEFAULT_ADMIN_PASSWORD = "P@ssw0rd";

        /// <summary>
        /// Seeds an initial admin user into the system if no users exist.
        /// Uses custom credentials from dataOptions if provided, otherwise falls back to default values.
        /// </summary>
        /// <param name="userManager">ASP.NET Identity user manager</param>
        /// <param name="userRegistrationService">Service handling user registration logic</param>
        /// <param name="dataOptions">Configuration options containing optional custom initial user credentials</param>
        public static async Task SeedInitialUser(
            UserManager<AppUser> userManager,
            IUserRegistrationService userRegistrationService,
            DataOptions dataOptions)
        {
            Guard.Against.Null(userManager);
            Guard.Against.Null(userRegistrationService);

            if (userManager.Users.Any())
            {
                return;
            }

            string email, password;
            if (dataOptions?.InitialUser != null)
            {
                email = dataOptions.InitialUser.Email;
                password = dataOptions.InitialUser.Password;
            }
            else
            {
                email = DEFAULT_ADMIN_EMAIL;
                password = DEFAULT_ADMIN_PASSWORD;
            }

            await userRegistrationService.RegisterUserAsync(email, password, CancellationToken.None);
        }
    }
}
