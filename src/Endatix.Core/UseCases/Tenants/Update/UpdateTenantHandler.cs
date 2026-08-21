using Endatix.Core.Abstractions.Data;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Entities = Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Tenants.Update;

/// <summary>
/// Handler for partially updating a tenant. The slug stays immutable: public sign-in and self-registration
/// URLs are built from it, so renaming a tenant never invalidates links already in the wild.
/// </summary>
public sealed class UpdateTenantHandler(
    IRepository<Entities.Tenant> tenantRepository,
    IRepository<Entities.TenantSettings> tenantSettingsRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateTenantCommand, Result<TenantDto>>
{
    /// <inheritdoc/>
    public async Task<Result<TenantDto>> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.SingleOrDefaultAsync(
            new TenantSpecifications.ByIdSpec(request.TenantId),
            cancellationToken);
        if (tenant is null)
        {
            return Result.NotFound("Tenant not found.");
        }

        var settings = await tenantSettingsRepository.SingleOrDefaultAsync(
            new TenantSpecifications.SettingsByTenantIdSpec(request.TenantId),
            cancellationToken);

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return Result.Invalid(TenantWriteRules.InvalidName(nameof(UpdateTenantCommand.Name)));
            }

            tenant.UpdateName(name);
        }

        if (request.Description is not null)
        {
            var description = request.Description.Trim();
            tenant.UpdateDescription(string.IsNullOrEmpty(description) ? null : description);
        }

        var selfRegistrationFailure = ApplySelfRegistrationPolicy(settings, request);
        if (selfRegistrationFailure is not null)
        {
            return selfRegistrationFailure;
        }

        // Tenant and settings are tracked by the same context, so one save covers both.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(TenantDto.FromEntity(tenant, settings));
    }

    /// <summary>
    /// Applies the self-registration fields that were provided, keeping the current value for the rest.
    /// Returns a failed result when the update cannot be applied, otherwise null.
    /// </summary>
    private static Result<TenantDto>? ApplySelfRegistrationPolicy(
        Entities.TenantSettings? settings,
        UpdateTenantCommand request)
    {
        var hasSelfRegistrationUpdate = request.AllowSelfRegistration.HasValue
            || request.AllowedAuthProviderKeys is not null
            || request.DefaultRegistrationRoleName is not null;
        if (!hasSelfRegistrationUpdate)
        {
            return null;
        }

        if (settings is null)
        {
            return Result.NotFound("Tenant settings not found.");
        }

        var registrationRole = string.IsNullOrWhiteSpace(request.DefaultRegistrationRoleName)
            ? settings.DefaultRegistrationRoleName
            : request.DefaultRegistrationRoleName.Trim();
        if (!Entities.TenantSettings.IsAllowedDefaultRegistrationRole(registrationRole))
        {
            return Result.Invalid(TenantWriteRules.ForbiddenRegistrationRole(
                registrationRole,
                nameof(UpdateTenantCommand.DefaultRegistrationRoleName)));
        }

        settings.UpdateSelfRegistrationPolicy(
            request.AllowSelfRegistration ?? settings.AllowSelfRegistration,
            request.AllowedAuthProviderKeys ?? settings.AllowedAuthProviderKeys,
            registrationRole);

        return null;
    }
}
