using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.TenantSettings.Get;
using MediatR;
using Entities = Endatix.Core.Entities;

namespace Endatix.Core.UseCases.TenantSettings.PartialUpdate;

/// <summary>
/// Handler for partially updating tenant settings.
/// </summary>
public sealed class PartialUpdateTenantSettingsHandler(
    IRepository<Entities.TenantSettings> repository,
    ITenantContext tenantContext,
    IMediator mediator)
    : ICommandHandler<PartialUpdateTenantSettingsCommand, Result<TenantSettingsDto>>
{
    /// <inheritdoc/>
    public async Task<Result<TenantSettingsDto>> Handle(
        PartialUpdateTenantSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await FindSettingsAsync(tenantContext.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result.NotFound("Tenant settings not found.");
        }

        var hasUpdates = false;

        if (request.RequireFolderAssignment.HasValue)
        {
            entity.UpdateRequireFolderAssignment(request.RequireFolderAssignment.Value);
            hasUpdates = true;
        }

        if (request.ClearSubmissionTokenExpiryHours)
        {
            entity.UpdateSubmissionTokenExpiry(null);
            hasUpdates = true;
        }
        else if (request.SubmissionTokenExpiryHours.HasValue)
        {
            entity.UpdateSubmissionTokenExpiry(request.SubmissionTokenExpiryHours);
            hasUpdates = true;
        }

        if (hasUpdates)
        {
            await repository.UpdateAsync(entity, cancellationToken);
        }

        return await mediator.Send(new GetTenantSettingsQuery(), cancellationToken);
    }

    private Task<Entities.TenantSettings?> FindSettingsAsync(long tenantId, CancellationToken ct) => tenantId > 0
        ? repository.FirstOrDefaultAsync(new TenantSettingsByTenantIdSpec(tenantId), ct)
        : Task.FromResult<Entities.TenantSettings?>(null);
}
