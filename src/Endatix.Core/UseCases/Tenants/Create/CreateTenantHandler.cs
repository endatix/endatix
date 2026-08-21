using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Common;
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
    IIdGenerator<long> idGenerator,
    IShortUrlGenerator shortUrlGenerator)
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

        var registrationRole = string.IsNullOrWhiteSpace(request.DefaultRegistrationRoleName)
            ? Entities.TenantSettings.DefaultRegistrationRole
            : request.DefaultRegistrationRoleName.Trim();
        var roleCheck = Entities.TenantSettings.ValidateDefaultRegistrationRole(registrationRole);
        if (!roleCheck.IsSuccess)
        {
            return Result.Invalid(roleCheck.ValidationErrors);
        }

        var shortUrl = await AllocateUniqueShortUrlAsync(cancellationToken);
        if (shortUrl is null)
        {
            return Result.Error("Could not allocate a unique tenant short URL. Retry the request.");
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            Entities.Tenant tenant = new(name, shortUrl, request.Description?.Trim())
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

    private async Task<string?> AllocateUniqueShortUrlAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < ShortUrl.CollisionRetries; attempt++)
        {
            var candidate = shortUrlGenerator.Create(ShortUrlKind.Standard);
            var taken = await tenantRepository.AnyAsync(
                new TenantSpecifications.ExistsByShortUrlSpec(candidate),
                cancellationToken);
            if (!taken)
            {
                return candidate;
            }
        }

        return null;
    }
}
