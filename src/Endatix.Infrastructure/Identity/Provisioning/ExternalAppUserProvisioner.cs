using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Provides AppUser provisioning functionality.
/// </summary>
/// <param name="identityDbContext">The identity database context.</param>
/// <param name="userManager">The user manager.</param>
/// <param name="logger">The logger.</param>
internal sealed class ExternalAppUserProvisioner(
    AppIdentityDbContext identityDbContext,
    UserManager<AppUser> userManager,
    ILogger<ExternalAppUserProvisioner> logger) : IExternalAppUserProvisioner
{
    private const int MAX_USER_NAME_LENGTH = 256;
    private const string DUPLICATE_EMAIL_MESSAGE = "Email already in use.";
    private const string APP_USER_EMAIL_REQUIRED_MESSAGE = "App user email is required.";
    private const string TENANT_REQUIRED_MESSAGE = "Tenant ID is required.";
    private const long NO_TENANT_ID = 0;
    private static readonly string[] _duplicateEmailIndexNames =
    [
        AppUser.UniqueConstraints.EmailPerTenant,
        "IX_AspNetUsers_TenantId_NormalizedEmail"
    ];
    private static readonly string[] _externalIdentityConstraintNames =
    [
        AppUser.UniqueConstraints.ExternalIdentityPerTenant,
        "UserNameIndex",
        "PK_UserLogins",
        "PK_AspNetUserLogins"
    ];

    public async Task<Result<AppUser>> ProvisionAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        IReadOnlyCollection<string> mappedAppRoles,
        ExternalIdentityProfile identityProfile,
        CancellationToken cancellationToken)
    {
        if (RejectRequest(tenantId, mappedAppRoles) is { } rejection)
        {
            return rejection;
        }

        if (RequireEmail(identityProfile) is null)
        {
            return Result<AppUser>.Invalid(new ValidationError(APP_USER_EMAIL_REQUIRED_MESSAGE));
        }

        var existingUser = await FindByExternalKeyAsync(tenantId, authProvider, externalSubjectId, cancellationToken);
        if (existingUser is not null)
        {
            return await ProcessExistingUserAsync(existingUser, externalSubjectId, identityProfile, mappedAppRoles);
        }

        var semaphore = ExternalProvisioningLock.Get(tenantId, authProvider, externalSubjectId);
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var user = await FindOrMoveUserWithoutTenantAsync(new(tenantId, authProvider, externalSubjectId), cancellationToken);
            if (user is not null)
            {
                return await ProcessExistingUserAsync(user, externalSubjectId, identityProfile, mappedAppRoles);
            }

            return await ProvisionNewUserAsync(
                tenantId,
                authProvider,
                externalSubjectId,
                identityProfile,
                mappedAppRoles,
                cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (IsDuplicateEmailConstraintViolation(ex))
            {
                logger.LogWarning(
                    ex,
                    "Rejected AppUser provisioning for provider {AuthProvider}, subject {ExternalSubjectId}, tenant {TenantId} because email is already in use.",
                    authProvider,
                    externalSubjectId,
                    tenantId);

                return Result<AppUser>.Conflict(DUPLICATE_EMAIL_MESSAGE);
            }

            if (IsExternalIdentityConstraintViolation(ex))
            {
                return await RecoverExternalIdentityRaceAsync(tenantId, authProvider, externalSubjectId, identityProfile, mappedAppRoles, ex, cancellationToken);
            }

            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static Result<AppUser>? RejectRequest(long tenantId, IReadOnlyCollection<string> mappedAppRoles)
    {
        if (tenantId <= 0)
        {
            return Result<AppUser>.Invalid(new ValidationError(TENANT_REQUIRED_MESSAGE));
        }

        if (mappedAppRoles.Count == 0)
        {
            return Result<AppUser>.NotFound("No mapped Hub AppUser roles.");
        }

        return null;
    }
    private Task<AppUser?> FindByExternalKeyAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        CancellationToken cancellationToken)
    {
        return identityDbContext.Users
            .FirstOrDefaultAsync(user =>
                user.TenantId == tenantId &&
                user.AuthProvider == authProvider &&
                user.ExternalSubjectId == externalSubjectId,
                cancellationToken);
    }

    /// <summary>
    /// Finds the user at the tenant. A user provisioned while Keycloak sign-ins ran without a tenant (tenant 0) is moved
    /// to the tenant on this sign-in instead of being created again; a user at another tenant is never moved.
    /// </summary>
    private async Task<AppUser?> FindOrMoveUserWithoutTenantAsync(ExternalKey key, CancellationToken cancellationToken)
    {
        return await FindByExternalKeyAsync(key.TenantId, key.AuthProvider, key.SubjectId, cancellationToken)
            ?? await MoveUserWithoutTenantAsync(key, cancellationToken);
    }

    /// <summary>
    /// Moves the user at tenant 0 through a tracked update, so the user's concurrency stamp guards it: when another
    /// sign-in moved the user first, the update fails and the user is read again at the tenant.
    /// </summary>
    private async Task<AppUser?> MoveUserWithoutTenantAsync(ExternalKey key, CancellationToken cancellationToken)
    {
        var user = await FindByExternalKeyAsync(NO_TENANT_ID, key.AuthProvider, key.SubjectId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        user.TenantId = key.TenantId;
        var updateResult = await userManager.UpdateAsync(user);
        if (updateResult.Succeeded)
        {
            logger.LogInformation("Moved external Hub AppUser {UserId} to tenant {TenantId}.", user.Id, key.TenantId);
            return user;
        }

        identityDbContext.Entry(user).State = EntityState.Detached;
        return await FindByExternalKeyAsync(key.TenantId, key.AuthProvider, key.SubjectId, cancellationToken);
    }

    private async Task<Result<AppUser>> ProcessExistingUserAsync(
        AppUser user,
        string externalSubjectId,
        ExternalIdentityProfile identityProfile,
        IReadOnlyCollection<string> mappedAppRoles)
    {
        try
        {
            if (await userManager.IsLockedOutAsync(user))
            {
                logger.LogWarning(
                    "Blocked locked-out external user {UserId} from authenticating via {AuthProvider}.",
                    user.Id,
                    user.AuthProvider);

                return Result<AppUser>.Forbidden("External user is locked out.");
            }

            var emailResult = await ApplyEmailAsync(user, identityProfile);
            if (!emailResult.IsSuccess)
            {
                return emailResult.ToErrorResult<AppUser>();
            }

            var userNameResult = await ApplyUserNameAsync(user, externalSubjectId);
            if (!userNameResult.IsSuccess)
            {
                return userNameResult.ToErrorResult<AppUser>();
            }

            user.DisplayName = identityProfile.DisplayName ?? user.DisplayName;
            user.LastLoginAt = DateTimeOffset.UtcNow;
            user.ExternalRolesJson = JsonSerializer.Serialize(mappedAppRoles.OrderBy(role => role).ToArray());

            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return ToErrorResult<AppUser>(updateResult);
            }

            return Result<AppUser>.Success(user);
        }
        catch (DbUpdateException ex) when (IsDuplicateEmailConstraintViolation(ex))
        {
            logger.LogWarning(
                ex,
                "Rejected AppUser profile refresh for user {UserId} because email is already in use.",
                user.Id);

            return Result<AppUser>.Conflict(DUPLICATE_EMAIL_MESSAGE);
        }
    }

    private async Task<Result<AppUser>> ProvisionNewUserAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        ExternalIdentityProfile identityProfile,
        IReadOnlyCollection<string> mappedAppRoles,
        CancellationToken cancellationToken)
    {
        var email = RequireEmail(identityProfile);
        if (email is null)
        {
            return Result<AppUser>.Invalid(new ValidationError(APP_USER_EMAIL_REQUIRED_MESSAGE));
        }

        AppUser user = new()
        {
            TenantId = tenantId,
            AuthProvider = authProvider,
            ExternalSubjectId = externalSubjectId,
            UserName = BuildExternalUserName(authProvider, externalSubjectId),
            Email = email,
            EmailConfirmed = true,
            DisplayName = identityProfile.DisplayName,
            LastLoginAt = DateTimeOffset.UtcNow,
            ExternalRolesJson = JsonSerializer.Serialize(mappedAppRoles.OrderBy(role => role).ToArray()),
            LockoutEnabled = true
        };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            var recoveredResult = await TryRecoverExternalIdentityRaceAsync(
                createResult,
                tenantId,
                authProvider,
                externalSubjectId,
                identityProfile,
                mappedAppRoles,
                cancellationToken);
            if (recoveredResult is not null)
            {
                return recoveredResult;
            }

            return ToErrorResult<AppUser>(createResult);
        }

        var loginResult = await userManager.AddLoginAsync(
            user,
            new UserLoginInfo(authProvider, externalSubjectId, authProvider));
        if (!loginResult.Succeeded)
        {
            var recoveredResult = await TryRecoverExternalIdentityRaceAsync(
                loginResult,
                tenantId,
                authProvider,
                externalSubjectId,
                identityProfile,
                mappedAppRoles,
                cancellationToken);
            if (recoveredResult is not null)
            {
                return recoveredResult;
            }

            return ToErrorResult<AppUser>(loginResult);
        }

        logger.LogInformation(
            "Provisioned external Hub AppUser {UserId} for provider {AuthProvider}, tenant {TenantId}.",
            user.Id,
            authProvider,
            tenantId);

        return Result<AppUser>.Success(user);
    }

    private async Task<Result<AppUser>?> TryRecoverExternalIdentityRaceAsync(
        IdentityResult result,
        long tenantId,
        string authProvider,
        string externalSubjectId,
        ExternalIdentityProfile identityProfile,
        IReadOnlyCollection<string> mappedAppRoles,
        CancellationToken cancellationToken)
    {
        if (!IsExternalIdentityConflict(result))
        {
            return null;
        }

        var recoveredUser = await FindByExternalKeyAsync(tenantId, authProvider, externalSubjectId, cancellationToken);
        return recoveredUser is null
            ? null
            : await ProcessExistingUserAsync(recoveredUser, externalSubjectId, identityProfile, mappedAppRoles);
    }

    private async Task<Result<AppUser>> RecoverExternalIdentityRaceAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        ExternalIdentityProfile identityProfile,
        IReadOnlyCollection<string> mappedAppRoles,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            exception,
            "Recovered AppUser provisioning race for provider {AuthProvider}, subject {ExternalSubjectId}, tenant {TenantId}.",
            authProvider,
            externalSubjectId,
            tenantId);

        var recoveredUser = await FindByExternalKeyAsync(tenantId, authProvider, externalSubjectId, cancellationToken);
        return recoveredUser is not null
            ? await ProcessExistingUserAsync(recoveredUser, externalSubjectId, identityProfile, mappedAppRoles)
            : Result<AppUser>.Error("Failed to provision external user.");
    }

    private static Result<T> ToErrorResult<T>(IdentityResult result)
    {
        return Result<T>.Error(new ErrorList(result.Errors.Select(error => error.Description)));
    }

    private static Result ToErrorResult(IdentityResult result)
    {
        return Result.Error(new ErrorList(result.Errors.Select(error => error.Description)));
    }

    private async Task<Result> ApplyEmailAsync(
        AppUser user,
        ExternalIdentityProfile identityProfile)
    {
        var email = RequireEmail(identityProfile);
        if (email is null)
        {
            return Result.Invalid(new ValidationError(APP_USER_EMAIL_REQUIRED_MESSAGE));
        }

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmailResult = await userManager.SetEmailAsync(user, email);
            if (!setEmailResult.Succeeded)
            {
                return ToErrorResult(setEmailResult);
            }
        }

        user.EmailConfirmed = true;
        return Result.Success();
    }

    private async Task<Result> ApplyUserNameAsync(AppUser user, string externalSubjectId)
    {
        var canonicalUserName = BuildExternalUserName(user.AuthProvider, externalSubjectId);
        if (string.Equals(user.UserName, canonicalUserName, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        var setUserNameResult = await userManager.SetUserNameAsync(user, canonicalUserName);
        if (!setUserNameResult.Succeeded)
        {
            return ToErrorResult(setUserNameResult);
        }

        return Result.Success();
    }

    internal static string BuildExternalUserName(string authProvider, string externalSubjectId)
    {
        var providerPart = ToAlphaNumeric(authProvider);
        var subjectPart = ToAlphaNumeric(externalSubjectId);
        var hashSuffix = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{authProvider}:{externalSubjectId}")))[..12];

        var maxSubjectLength = Math.Max(0, MAX_USER_NAME_LENGTH - providerPart.Length - hashSuffix.Length);
        if (subjectPart.Length > maxSubjectLength)
        {
            subjectPart = subjectPart[..maxSubjectLength];
        }

        return $"{providerPart}{subjectPart}{hashSuffix}";
    }

    private static string ToAlphaNumeric(string value)
    {
        string alphaNumeric = new(value.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(alphaNumeric) ? "External" : alphaNumeric;
    }

    private readonly record struct ExternalKey(long TenantId, string AuthProvider, string SubjectId);

    private static string? RequireEmail(ExternalIdentityProfile identityProfile)
    {
        return string.IsNullOrWhiteSpace(identityProfile.Email) ? null : identityProfile.Email.Trim();
    }

    private static bool IsDuplicateEmailConstraintViolation(DbUpdateException dbUpdateException)
    {
        return _duplicateEmailIndexNames.Any(indexName =>
            dbUpdateException.ToString().Contains(indexName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExternalIdentityConstraintViolation(DbUpdateException dbUpdateException)
    {
        return _externalIdentityConstraintNames.Any(constraintName =>
            dbUpdateException.ToString().Contains(constraintName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExternalIdentityConflict(IdentityResult result)
    {
        return result.Errors.Any(error =>
            error.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) ||
            error.Code.Contains("LoginAlreadyAssociated", StringComparison.OrdinalIgnoreCase));
    }
}
