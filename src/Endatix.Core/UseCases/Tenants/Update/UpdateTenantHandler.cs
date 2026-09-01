using Endatix.Core.Abstractions.Data;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Entities = Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Tenants.Update;

/// <summary>
/// Handler for partially updating a tenant. The short URL is immutable.
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

        var patchResult = ValidatePatch(request, settings);
        if (!patchResult.IsSuccess)
        {
            return patchResult.ToErrorResult<TenantDto>();
        }

        ApplyPatch(tenant, settings, request, patchResult.Value);
        tenant.RaiseUpdated(settings);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(TenantDto.FromEntity(tenant, settings));
    }

    private static Result<TenantPatch> ValidatePatch(
        UpdateTenantCommand request,
        Entities.TenantSettings? settings)
    {
        string? name = null;
        if (request.Name is not null)
        {
            name = request.Name.Trim();
            if (name.Length == 0)
            {
                return Result.Invalid(TenantWriteRules.InvalidName(nameof(UpdateTenantCommand.Name)));
            }
        }

        var updatesSelfRegistration = request.AllowSelfRegistration.HasValue
            || request.AllowedAuthProviderKeys is not null
            || request.DefaultRegistrationRoleName is not null;
        if (!updatesSelfRegistration)
        {
            return Result.Success(new TenantPatch(name, null));
        }

        if (settings is null)
        {
            return Result.NotFound("Tenant settings not found.");
        }

        var registrationRole = string.IsNullOrWhiteSpace(request.DefaultRegistrationRoleName)
            ? settings.DefaultRegistrationRoleName
            : request.DefaultRegistrationRoleName.Trim();
        var roleCheck = Entities.TenantSettings.ValidateDefaultRegistrationRole(registrationRole);
        if (!roleCheck.IsSuccess)
        {
            return Result.Invalid(roleCheck.ValidationErrors);
        }

        return Result.Success(new TenantPatch(name, registrationRole));
    }

    private static void ApplyPatch(
        Entities.Tenant tenant,
        Entities.TenantSettings? settings,
        UpdateTenantCommand request,
        TenantPatch patch)
    {
        if (patch.Name is not null)
        {
            tenant.UpdateName(patch.Name);
        }

        if (request.Description is not null)
        {
            var description = request.Description.Trim();
            tenant.UpdateDescription(description.Length == 0 ? null : description);
        }

        if (settings is not null && patch.RegistrationRole is not null)
        {
            settings.UpdateSelfRegistrationPolicy(
                request.AllowSelfRegistration ?? settings.AllowSelfRegistration,
                request.AllowedAuthProviderKeys ?? settings.AllowedAuthProviderKeys,
                patch.RegistrationRole);
        }
    }

    private readonly record struct TenantPatch(string? Name, string? RegistrationRole);
}
