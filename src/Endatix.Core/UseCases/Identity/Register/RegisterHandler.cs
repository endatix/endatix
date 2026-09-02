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
    IRepository<Entities.Tenant> tenantRepository,
    IRepository<Entities.TenantSettings> tenantSettingsRepository,
    IRoleManagementService roleManagementService,
    IMediator mediator
    ) : ICommandHandler<RegisterCommand, Result<string>>
{
    public const string TenantNotFoundMessage = "Tenant not found.";
    public const string SelfRegistrationDisabledMessage = "Self-registration is not enabled for this tenant.";

    /// <summary>
    /// Returned whether or not the address was free, so registration cannot be used to enumerate
    /// accounts. Mirrors <c>ForgotPasswordHandler.GENERAL_SUCCESS_MESSAGE</c>.
    /// </summary>
    public const string GENERAL_SUCCESS_MESSAGE = "Thank you. If this email address can be registered, you will receive an email with instructions to verify it.";

    /// <inheritdoc />
    public async Task<Result<string>> Handle(RegisterCommand request, CancellationToken cancellationToken)
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

        // ValidateDefaultRegistrationRole only rejects roles that must never be used, so resolve the
        // name too: assigning after the user exists would leave an unusable account no retry can fix.
        var roleResolution = await ResolveRegistrationRoleAsync(registrationRole, tenant.Id, cancellationToken);
        if (!roleResolution.IsSuccess)
        {
            return roleResolution.ToErrorResult<string>();
        }

        var registerResult = await userRegistrationService.RegisterTenantUserAsync(
            request.Email,
            request.Password,
            tenant.Id,
            registrationRole,
            cancellationToken);

        return await CompleteRegistrationAsync(registerResult, cancellationToken);
    }

    private async Task<Result> ResolveRegistrationRoleAsync(
        string registrationRole,
        long tenantId,
        CancellationToken cancellationToken)
    {
        var missingResult = await roleManagementService.GetMissingAssignableRoleNamesAsync(
            [registrationRole],
            tenantId,
            cancellationToken);
        if (!missingResult.IsSuccess)
        {
            return Result.Error(new ErrorList(missingResult.Errors, missingResult.CorrelationId));
        }

        if (missingResult.Value is { Count: > 0 })
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(Entities.TenantSettings.DefaultRegistrationRoleName),
                ErrorMessage = $"'{registrationRole}' is not an assignable role for this tenant."
            });
        }

        return Result.Success();
    }

    private async Task<Result<string>> RegisterUnattachedAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var registerResult = await userRegistrationService.RegisterUserAsync(email, password, cancellationToken);

        return await CompleteRegistrationAsync(registerResult, cancellationToken);
    }

    /// <summary>
    /// Collapses "created" and "address already taken" (<see cref="ResultStatus.NoContent"/>) into one
    /// response. Only a real registration raises <see cref="UserRegisteredEvent"/>.
    /// </summary>
    private async Task<Result<string>> CompleteRegistrationAsync(
        Result<User> registerResult,
        CancellationToken cancellationToken)
    {
        if (!registerResult.IsSuccess)
        {
            return registerResult.ToErrorResult<string>();
        }

        if (registerResult.Value is { } user)
        {
            await mediator.Publish(new UserRegisteredEvent(user), cancellationToken);
        }

        return Result<string>.Success(GENERAL_SUCCESS_MESSAGE);
    }
}
