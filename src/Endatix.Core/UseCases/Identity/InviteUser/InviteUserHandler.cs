using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.InviteUser;

/// <summary>
/// Handler for the <see cref="InviteUserCommand"/> class.
/// </summary>
public sealed class InviteUserHandler(
    IUserRegistrationService userRegistrationService,
    IRoleManagementService roleManagementService,
    ITenantContext tenantContext,
    ICurrentUserAuthorizationService currentUserAuthorizationService)
    : ICommandHandler<InviteUserCommand, Result<User>>
{
    /// <inheritdoc/>
    public async Task<Result<User>> Handle(
        InviteUserCommand request,
        CancellationToken cancellationToken)
    {
        if (request.RoleNames.Count > 0)
        {
            var rolesGuard = await ValidateRequestedRolesAsync(request.RoleNames, cancellationToken);
            if (!rolesGuard.IsSuccess)
            {
                return rolesGuard.ToErrorResult<User>();
            }
        }

        var registerResult = await userRegistrationService.RegisterInvitedUserAsync(
            request.Email,
            tenantContext.TenantId,
            cancellationToken);

        if (!registerResult.IsSuccess || registerResult.Value is null)
        {
            return registerResult;
        }

        foreach (var roleName in request.RoleNames)
        {
            var assignResult = await roleManagementService.AssignRoleToUserAsync(
                registerResult.Value.Id,
                roleName,
                cancellationToken);

            if (!assignResult.IsSuccess)
            {
                return assignResult.ToErrorResult<User>();
            }
        }

        return Result.Success(registerResult.Value);
    }


    private async Task<Result> ValidateRequestedRolesAsync(
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken)
    {
        var platformScopedRoles = roleNames
            .Where(SystemRole.IsPlatformScopedRole)
            .ToList();

        if (platformScopedRoles.Count > 0)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(InviteUserCommand.RoleNames),
                ErrorMessage = $"The following roles cannot be assigned from tenant user invitations: {string.Join(", ", platformScopedRoles)}"
            });
        }

        if (roleNames.Any(roleName => string.Equals(roleName, SystemRole.Admin.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(InviteUserCommand.RoleNames),
                ErrorMessage = "Admin access can be assigned after the invited user verifies their account."
            });
        }

        var manageUsersResult = await currentUserAuthorizationService.ValidateAccessAsync(
            Actions.Tenant.ManageUsers,
            cancellationToken);

        if (!manageUsersResult.IsSuccess)
        {
            return manageUsersResult;
        }

        var rolesResult = await roleManagementService.ListRolesAsync(0, int.MaxValue, null, null, cancellationToken);
        if (!rolesResult.IsSuccess)
        {
            return Result.Error(new ErrorList(rolesResult.Errors, rolesResult.CorrelationId));
        }

        var availableRoles = rolesResult.Value!.Items
            .Select(role => role.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingRoles = roleNames
            .Where(roleName => !availableRoles.Contains(roleName))
            .ToList();

        if (missingRoles.Count > 0)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(InviteUserCommand.RoleNames),
                ErrorMessage = $"The following roles do not exist: {string.Join(", ", missingRoles)}"
            });
        }

        return Result.Success();
    }
}
