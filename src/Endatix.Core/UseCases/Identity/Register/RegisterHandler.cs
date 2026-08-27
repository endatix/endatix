using Endatix.Core.Abstractions;
using Endatix.Core.Common;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Tenants;
using MediatR;
using Entities = Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Identity.Register;

/// <summary>
/// Handles the registration of a new user.
/// </summary>
public class RegisterHandler(
    IUserRegistrationService userRegistrationService,
    IUserService userService,
    IRepository<Entities.Tenant> tenantRepository,
    IRepository<Entities.TenantSettings> tenantSettingsRepository,
    IRoleManagementService roleManagementService,
    IMediator mediator
    ) : ICommandHandler<RegisterCommand, Result<User>>
{
    public const string TenantNotFoundMessage = "Tenant not found.";
    public const string SelfRegistrationDisabledMessage = "Self-registration is not enabled for this tenant.";
    public const string EmailAlreadyRegisteredMessage = "The email is already registered.";

    /// <inheritdoc />
    public async Task<Result<User>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantSlug))
        {
            return await RegisterUnattachedAsync(request.Email, request.Password, cancellationToken);
        }

        if (!PublicId.IsValidTenantSlug(request.TenantSlug))
        {
            return Result.NotFound(TenantNotFoundMessage);
        }

        var tenant = await tenantRepository.SingleOrDefaultAsync(
            new TenantSpecifications.LiveBySlugSpec(request.TenantSlug),
            cancellationToken);
        if (tenant is null)
        {
            return Result.NotFound(TenantNotFoundMessage);
        }

        var settings = await tenantSettingsRepository.SingleOrDefaultAsync(
            new TenantSpecifications.SettingsByTenantIdSpec(tenant.Id),
            cancellationToken);
        if (settings is null || !settings.AllowSelfRegistration)
        {
            return Result.Forbidden(SelfRegistrationDisabledMessage);
        }

        var registrationRole = string.IsNullOrWhiteSpace(settings.DefaultRegistrationRoleName)
            ? Entities.TenantSettings.DefaultRegistrationRole
            : settings.DefaultRegistrationRoleName;
        if (!Entities.TenantSettings.IsAllowedDefaultRegistrationRole(registrationRole))
        {
            return Result.Invalid(TenantWriteRules.ForbiddenRegistrationRole(
                registrationRole,
                nameof(Entities.TenantSettings.DefaultRegistrationRoleName)));
        }

        // Self-registration is anonymous, so it may only ever create a new account. Letting it fall
        // through to RegisterUserAsync for an address that already exists would take the existing
        // branches for an existing user: an unattached account would be silently moved into this
        // tenant, and either way the account would then be granted the tenant's registration role
        // below - a cross-tenant takeover driven purely by knowing someone's email address.
        var existingUser = await userService.GetUserAsync(request.Email, cancellationToken);
        if (existingUser.IsSuccess)
        {
            return Result.Invalid(new ValidationError(EmailAlreadyRegisteredMessage));
        }

        var registerResult = await userRegistrationService.RegisterUserAsync(
            request.Email,
            request.Password,
            tenant.Id,
            isEmailConfirmed: false,
            cancellationToken);

        if (!registerResult.IsSuccess || registerResult.Value is null)
        {
            return registerResult;
        }

        var assignResult = await roleManagementService.AssignRoleToUserAsync(
            registerResult.Value.Id,
            registrationRole,
            tenant.Id,
            cancellationToken);
        if (!assignResult.IsSuccess)
        {
            return assignResult.ToErrorResult<User>();
        }

        await mediator.Publish(new UserRegisteredEvent(registerResult.Value), cancellationToken);
        return registerResult;
    }

    private async Task<Result<User>> RegisterUnattachedAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var registerResult = await userRegistrationService.RegisterUserAsync(email, password, cancellationToken);

        if (registerResult.IsSuccess && registerResult.Value is { } user)
        {
            await mediator.Publish(new UserRegisteredEvent(user), cancellationToken);
        }

        return registerResult;
    }
}
