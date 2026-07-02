using Endatix.Framework.Logging;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Seed;

/// <summary>
/// Source-generated logging for initial identity seeding.
/// </summary>
internal static partial class IdentitySeedLoggerExtensions
{
    [LoggerMessage(
        EventId = EndatixEventIds.IdentitySeed.CredentialsInConfig,
        Level = LogLevel.Warning,
        Message = "🔐 Initial user credentials are still present in the configuration and are no longer needed. Remove them from the configuration file to prevent their exposure. For more info check https://docs.endatix.com/docs/getting-started/installation")]
    public static partial void LogInitialUserCredentialsInConfig(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.IdentitySeed.RegistrationFailed,
        Level = LogLevel.Error,
        Message = "❌ Failed to register initial user {Email}. Errors: {Errors}. ValidationErrors: {ValidationErrors}")]
    public static partial void LogInitialUserRegistrationFailed(
        this ILogger logger,
        string email,
        string errors,
        string validationErrors);

    [LoggerMessage(
        EventId = EndatixEventIds.IdentitySeed.RoleAssignmentFailed,
        Level = LogLevel.Error,
        Message = "❌ Failed to assign role to initial user {Email}. Errors: {Errors}. ValidationErrors: {ValidationErrors}")]
    public static partial void LogInitialUserRoleAssignmentFailed(
        this ILogger logger,
        string email,
        string errors,
        string validationErrors);

    [LoggerMessage(
        EventId = EndatixEventIds.IdentitySeed.UserCreated,
        Level = LogLevel.Information,
        Message = "👤 Initial user {Email} created successfully! Please use it to authenticate.")]
    public static partial void LogInitialUserCreated(this ILogger logger, string email);

    [LoggerMessage(
        EventId = EndatixEventIds.IdentitySeed.PasswordInConfig,
        Level = LogLevel.Warning,
        Message = "🔐 The default password can be found in the configuration file under Endatix:Data:InitialUser. Please change the password after logging in and delete the InitialUser section from the configuration file.")]
    public static partial void LogInitialUserPasswordInConfig(this ILogger logger);
}
