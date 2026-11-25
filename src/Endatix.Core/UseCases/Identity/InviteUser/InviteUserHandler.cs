using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.Identity.InviteUser;

/// <summary>
/// Handler for the <see cref="InviteUserCommand"/> class.
/// </summary>
public sealed class InviteUserHandler(
    IUserRegistrationService userRegistrationService,
    IRoleManagementService roleManagementService,
    ITenantContext tenantContext,
    ICurrentUserAuthorizationService currentUserAuthorizationService,
    AccountStanding standing,
    ISender sender)
    : ICommandHandler<InviteUserCommand, Result<User>>
{
    /// <inheritdoc/>
    public async Task<Result<User>> Handle(
        InviteUserCommand request,
        CancellationToken cancellationToken)
    {
        var inviteUsersResult = await currentUserAuthorizationService.ValidateAccessAsync(
            Actions.Tenant.InviteUsers,
            cancellationToken);

        if (!inviteUsersResult.IsSuccess)
        {
            return inviteUsersResult.ToErrorResult<User>();
        }

        if (request.RoleNames.Count > 0)
        {
            var rolesGuard = await ValidateRequestedRolesAsync(request.RoleNames, cancellationToken);
            if (!rolesGuard.IsSuccess)
            {
                return rolesGuard.ToErrorResult<User>();
            }
        }

        return await InviteRefusalAsync(request.Email, cancellationToken) ?? await RegisterAndAssignRolesAsync(request, cancellationToken);
    }

    private async Task<Result<User>?> InviteRefusalAsync(string email, CancellationToken ct)
    {
        if (!standing.IsEnforced)
        {
            return null;
        }

        var eligible = await sender.Send(new CheckInviteTarget(email, tenantContext.TenantId), ct);
        return eligible.IsSuccess ? await standing.RefusalAsync<User>(ct) : eligible.ToErrorResult<User>();
    }

    private async Task<Result<User>> RegisterAndAssignRolesAsync(InviteUserCommand request, CancellationToken cancellationToken)
    {
        var registerResult = await userRegistrationService.RegisterInvitedUserAsync(
            request.Email,
            tenantContext.TenantId,
            cancellationToken);

        if (!registerResult.IsSuccess || registerResult.Value is null)
        {
            return registerResult;
        }

        return await AssignRolesAsync(registerResult.Value, request.RoleNames, cancellationToken);
    }

    private async Task<Result<User>> AssignRolesAsync(User user, IReadOnlyList<string> roleNames, CancellationToken cancellationToken)
    {
        var assignedRoleNames = new List<string>();
        foreach (var roleName in roleNames)
        {
            var assignResult = await roleManagementService.AssignRoleToUserAsync(user.Id, roleName, cancellationToken);

            if (!assignResult.IsSuccess)
            {
                await RollbackAssignedRolesAsync(user.Id, assignedRoleNames, cancellationToken);
                return assignResult.ToErrorResult<User>();
            }

            assignedRoleNames.Add(roleName);
        }

        return Result.Success(user);
    }

    private async Task RollbackAssignedRolesAsync(
        long userId,
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken)
    {
        foreach (var roleName in roleNames)
        {
            await roleManagementService.RemoveRoleFromUserAsync(userId, roleName, cancellationToken);
        }
    }

    private async Task<Result> ValidateRequestedRolesAsync(
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken)
    {
        var platformAdminRoles = roleNames
            .Where(SystemRole.IsPlatformAdminRoleName)
            .ToList();

        if (platformAdminRoles.Count > 0)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(InviteUserCommand.RoleNames),
                ErrorMessage = $"The following roles cannot be assigned from tenant user invitations: {string.Join(", ", platformAdminRoles)}"
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

        var missingRolesResult = await roleManagementService.GetMissingAssignableRoleNamesAsync(
            roleNames,
            cancellationToken);
        if (!missingRolesResult.IsSuccess)
        {
            return Result.Error(new ErrorList(missingRolesResult.Errors, missingRolesResult.CorrelationId));
        }

        var missingRoles = missingRolesResult.Value ?? [];
        if (missingRoles.Count > 0)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(InviteUserCommand.RoleNames),
                ErrorMessage = $"The following roles do not exist: {string.Join(", ", missingRoles)}"
            });
        }

        return await roleManagementService.EnsureCurrentUserCanGrantRolesAsync(roleNames, cancellationToken);
    }
}
