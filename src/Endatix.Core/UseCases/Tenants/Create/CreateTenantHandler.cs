using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Entities = Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Tenants.Create;

/// <summary>
/// Handler for creating a tenant and its settings row.
/// </summary>
public sealed class CreateTenantHandler(
    IRepository<Entities.Tenant> tenantRepository,
    IRepository<Entities.TenantSettings> tenantSettingsRepository,
    IUnitOfWork unitOfWork,
    IIdGenerator<long> idGenerator)
    : ICommandHandler<CreateTenantCommand, Result<TenantDto>>
{
    /// <inheritdoc/>
    public async Task<Result<TenantDto>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return Result.Invalid(TenantWriteRules.InvalidName(nameof(CreateTenantCommand.Name)));
        }

        var slugResult = TenantWriteRules.NormalizeSlug(request.Slug, name);
        if (!slugResult.IsSuccess)
        {
            return Result.Invalid(TenantWriteRules.InvalidSlug(
                slugResult.Errors.FirstOrDefault() ?? "Slug is invalid.",
                nameof(CreateTenantCommand.Slug)));
        }

        var slug = slugResult.Value;
        var registrationRole = string.IsNullOrWhiteSpace(request.DefaultRegistrationRoleName)
            ? Entities.TenantSettings.DefaultRegistrationRole
            : request.DefaultRegistrationRoleName.Trim();
        if (!Entities.TenantSettings.IsAllowedDefaultRegistrationRole(registrationRole))
        {
            return Result.Invalid(TenantWriteRules.ForbiddenRegistrationRole(
                registrationRole,
                nameof(CreateTenantCommand.DefaultRegistrationRoleName)));
        }

        var slugTaken = await tenantRepository.AnyAsync(
            new TenantSpecifications.ExistsBySlugSpec(slug),
            cancellationToken);
        if (slugTaken)
        {
            return Result.Invalid(TenantWriteRules.DuplicateSlug(slug, nameof(CreateTenantCommand.Slug)));
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            Entities.Tenant tenant = new(name, slug, request.Description?.Trim())
            {
                // The settings row keys off the tenant id, so it is assigned up front rather than
                // stamped by the context on save.
                Id = idGenerator.CreateId()
            };

            // tenant.created is captured to the outbox inside the save below; there are no in-process
            // MediatR subscribers, so nothing is published here.
            tenant.RaiseCreated();
            await tenantRepository.AddAsync(tenant, cancellationToken);

            Entities.TenantSettings settings = new(tenant.Id);
            settings.UpdateSelfRegistrationPolicy(
                request.AllowSelfRegistration,
                request.AllowedAuthProviderKeys,
                registrationRole);
            await tenantSettingsRepository.AddAsync(settings, cancellationToken);

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result<TenantDto>.Created(TenantDto.FromEntity(tenant, settings));
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
