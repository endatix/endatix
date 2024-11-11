using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Seed
{
    /// <summary>
    /// Provides functionality for seeding initial identity data in the application
    /// </summary>
    public static class IdentitySeed
    {
        private const string DEFAULT_ADMIN_EMAIL = "admin@endatix.com";
        private const string DEFAULT_ADMIN_PASSWORD = "P@ssw0rd";

        private const string LOG_INDENT = "\t\t\t";

        /// <summary>
        /// Seeds an initial admin user into the system if no users exist.
        /// Uses custom credentials from dataOptions if provided, otherwise falls back to default values.
        /// </summary>
        /// <param name="userManager">ASP.NET Identity user manager</param>
        /// <param name="userRegistrationService">Service handling user registration logic</param>
        /// <param name="dataOptions">Configuration options containing optional custom initial user credentials</param>
        /// <param name="logger">Logger instance</param>
        public static async Task SeedInitialUser(
            UserManager<AppUser> userManager,
            IUserRegistrationService userRegistrationService,
            DataOptions dataOptions,
            ILogger logger)
        {
            Guard.Against.Null(userManager);
            Guard.Against.Null(userRegistrationService);

            var initialUserIsConfigured = dataOptions?.InitialUser != null;

            if (userManager.Users.Any())
            {
                if (initialUserIsConfigured)
                {
                    logger.LogWarning("🔐 Initial user credentials are still present in the configuration and are not longer needed\r\n" +
                        $"{LOG_INDENT}| Remove them from the configuration file to prevent their exposure to the outside world.\r\n" +
                        $"{LOG_INDENT}| For more info check https://docs.endatix.com/docs/getting-started/installation");
                }

                return;
            }

            string email, password;
            if (initialUserIsConfigured)
            {
                email = dataOptions!.InitialUser!.Email;
                password = dataOptions!.InitialUser!.Password;
            }
            else
            {
                email = DEFAULT_ADMIN_EMAIL;
                password = DEFAULT_ADMIN_PASSWORD;
            }

            await userRegistrationService.RegisterUserAsync(email, password, CancellationToken.None);
            logger.LogInformation($"👤 Initial user {email} created successfully! Please use it to authenticate.");
        }
    }
}
