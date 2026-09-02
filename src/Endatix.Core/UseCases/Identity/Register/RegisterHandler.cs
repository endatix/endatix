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

        var shortUrl = ShortUrl.Normalize(request.TenantSlug);
        if (shortUrl is null || !ShortUrl.IsValid(shortUrl))
        {
            return Result.NotFound(TenantNotFoundMessage);
        }

        var tenant = await tenantRepository.SingleOrDefaultAsync(
            new TenantSpecifications.LiveByShortUrlSpec(shortUrl),
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
        var roleCheck = Entities.TenantSettings.ValidateDefaultRegistrationRole(registrationRole);
        if (!roleCheck.IsSuccess)
        {
            return Result.Invalid(roleCheck.ValidationErrors);
        }

        // Anonymous self-reg must not attach or re-role an existing account (email-oracle takeover).
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
