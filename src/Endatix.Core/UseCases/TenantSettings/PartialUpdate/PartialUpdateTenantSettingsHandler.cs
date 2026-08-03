using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.TenantSettings.Get;
using Entities = Endatix.Core.Entities;
using MediatR;

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
        var spec = new TenantSettingsByTenantIdSpec(tenantContext.TenantId);
        var entity = await repository.FirstOrDefaultAsync(spec, cancellationToken);
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

        // TODO(docs/todo-use-union-for-partial-updates.md): replace clear+value ceremony with UpdateOrReset<T>.
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
}
