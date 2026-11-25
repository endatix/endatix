using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Identity.Users;

public sealed partial class AppUserRegistrationService
{
    internal async Task<Result<User>> RegisterTeammateAsync(string email, long tenantId, AccountStanding standing, CancellationToken ct)
    {
        var target = await ResolveRegistrationTargetAsync(email, tenantId, isAnonymous: false);
        return target.Refusal ?? await standing.RefusalAsync<User>(ct)
            ?? await RegisterEligibleUserAsync(target, GenerateTemporaryInvitePassword(), isEmailConfirmed: false, sendInvitationEmail: true, ct);
    }

    private async Task<RegistrationTarget> ResolveRegistrationTargetAsync(string email, long tenantId, bool isAnonymous)
    {
        if (!userManager.SupportsUserEmail)
        {
            throw new NotSupportedException("Registration logic requires a user store with email support. Please check your email settings");
        }
        var valid = ValidateRegistrationEmail(email);
        if (!valid.IsSuccess)
        {
            return new(email, tenantId, null, valid.ToErrorResult<User>());
        }
        var trimmed = email.Trim();
        var existing = await userManager.FindByEmailAsync(trimmed);
        var error = ExistingTargetError(existing, tenantId);
        return new(trimmed, tenantId, existing, RegistrationRefusal(trimmed, error, isAnonymous));
    }

    private Result<User>? RegistrationRefusal(string email, string? error, bool isAnonymous)
    {
        if (error is null)
        {
            return null;
        }
        return isAnonymous ? ExistingAccountSuppressed(email) : Result.Invalid(new ValidationError(error));
    }

    private static string? ExistingTargetError(AppUser? user, long tenantId)
    {
        if (user is null)
        {
            return null;
        }
        if (user.TenantId > 0 && user.TenantId != tenantId)
        {
            return EMAIL_ALREADY_REGISTERED_MESSAGE;
        }
        return user.TenantId == tenantId && user.EmailConfirmed ? USER_ALREADY_BELONGS_TO_TENANT_MESSAGE : null;
    }

    private sealed record RegistrationTarget(string Email, long TenantId, AppUser? Existing, Result<User>? Refusal);
}
