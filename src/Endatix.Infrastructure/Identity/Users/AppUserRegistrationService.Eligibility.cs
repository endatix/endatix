using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.InviteUser;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Identity.Users;

public sealed partial class AppUserRegistrationService
{
    private static string? ExistingTargetError(long currentTenant, bool confirmed, long requestedTenant)
    {
        if (currentTenant > 0 && currentTenant != requestedTenant)
        {
            return EMAIL_ALREADY_REGISTERED_MESSAGE;
        }
        return currentTenant == requestedTenant && confirmed ? USER_ALREADY_BELONGS_TO_TENANT_MESSAGE : null;
    }

    internal sealed class CheckInviteTargetHandler(UserManager<AppUser> users) : IQueryHandler<CheckInviteTarget, Result>
    {
        public async Task<Result> Handle(CheckInviteTarget request, CancellationToken cancellationToken)
        {
            var valid = ValidateRegistrationEmail(request.Email);
            if (!valid.IsSuccess)
            {
                return valid;
            }
            var normalized = users.NormalizeEmail(request.Email.Trim());
            var target = await users.Users.AsNoTracking().Where(user => user.NormalizedEmail == normalized)
                .Select(user => new { user.TenantId, user.EmailConfirmed }).SingleOrDefaultAsync(cancellationToken);
            return target is not null && ExistingTargetError(target.TenantId, target.EmailConfirmed, request.TenantId) is { } error
                ? Result.Invalid(new ValidationError(error)) : Result.Success();
        }
    }
}
