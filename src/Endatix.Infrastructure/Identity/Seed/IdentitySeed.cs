using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Logging;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity.Authentication;
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
        /// <param name="roleManagementService">Service handling assigning roles to the seeded user</param>
        /// <param name="dataOptions">Configuration options containing optional custom initial user credentials</param>
        /// <param name="logger">Logger instance</param>
        public static async Task SeedInitialUser(
            UserManager<AppUser> userManager,
            IUserRegistrationService userRegistrationService,
            IRoleManagementService roleManagementService,
            DataOptions dataOptions,
            ILogger logger)
        {
            Guard.Against.Null(userManager);
            Guard.Against.Null(userRegistrationService);
            Guard.Against.Null(roleManagementService);

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

            var registerUserResult = await userRegistrationService.RegisterUserAsync(
                email,
                password,
                tenantId: AuthConstants.DEFAULT_ADMIN_TENANT_ID,
                isEmailConfirmed: true,             // Initial user should have confirmed email
                CancellationToken.None);

            if (!registerUserResult.IsSuccess)
            {
                logger.LogError(
                                    "❌ Failed to register initial user {Email}. Errors: {Errors}. ValidationErrors: {ValidationErrors}",
                                    SensitiveValue.Email(email),
                                    string.Join(", ", registerUserResult.Errors!),
                                    string.Join(", ", registerUserResult.ValidationErrors!));
                return;
            }

            var assignRoleResult = await roleManagementService.AssignRoleToUserAsync(registerUserResult.Value.Id, SystemRole.PlatformAdmin.Name);

            if (!assignRoleResult.IsSuccess)
            {
                logger.LogError(
                                    "❌ Failed to assign role to initial user {Email}. Errors: {Errors}. ValidationErrors: {ValidationErrors}",
                                    email,
                                    string.Join(", ", assignRoleResult.Errors!),
                                    string.Join(", ", assignRoleResult.ValidationErrors!));
                return;
            }

            logger.LogInformation("👤 Initial user {Email} created successfully! Please use it to authenticate.", SensitiveValue.Email(email));
            logger.LogWarning("🔐 The default password can be found in the configuration file under Endatix:Data:InitialUser. Please change the password after logging in and delete the InitialUser section from the configuration file.");
        }
    }
}
