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
    IShortUrlGenerator shortUrlGenerator,
    IUniqueConstraintViolationChecker uniqueConstraintViolationChecker)
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

        // The pre-check only narrows the field - the unique index is the authority, since a
        // concurrent create can take the candidate between the two. Both outcomes spend one draw.
        for (var attempt = 0; attempt < ShortUrl.CollisionRetries; attempt++)
        {
            var shortUrl = shortUrlGenerator.Create(ShortUrlKind.Standard);
            var taken = await tenantRepository.AnyAsync(
                new TenantSpecifications.ExistsByShortUrlSpec(shortUrl),
                cancellationToken);
            if (taken)
            {
                continue;
            }

            var tenant = await TryProvisionAsync(request, name, shortUrl, registrationRole, cancellationToken);
            if (tenant is not null)
            {
                return Result<TenantDto>.Created(tenant);
            }
        }

        return Result<TenantDto>.Unavailable("Could not allocate a unique tenant short URL. Retry the request.");
    }

    /// <summary>
    /// Persists the tenant and its settings in one transaction. Returns null when the unique index
    /// rejected <paramref name="shortUrl"/>, meaning the caller should draw another candidate.
    /// </summary>
    private async Task<TenantDto?> TryProvisionAsync(
        CreateTenantCommand request,
        string name,
        string shortUrl,
        string registrationRole,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            Entities.Tenant tenant = new(name, shortUrl, request.Description?.Trim())
            {
                Id = idGenerator.CreateId()
            };

            tenant.RaiseCreated();
            await tenantRepository.AddAsync(tenant, cancellationToken);

            Entities.TenantSettings settings = new(tenant.Id);
            settings.UpdateSelfRegistrationPolicy(
                request.AllowSelfRegistration,
                request.AllowedAuthProviderKeys,
                registrationRole);
            await tenantSettingsRepository.AddAsync(settings, cancellationToken);

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            return TenantDto.FromEntity(tenant, settings);
        }
        catch (Exception exception)
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);

            var violation = uniqueConstraintViolationChecker.AnalyzeUniqueConstraint(exception);
            if (violation.IsUniqueConstraintViolation && violation.IsTenantShortUrlViolation())
            {
                return null;
            }

            throw;
        }
    }
}
