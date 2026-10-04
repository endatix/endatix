using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Features.Properties;

/// <summary>
/// Soft-deletes a property and its value cells on this form.
/// </summary>
public sealed record DeletePropertyCommand(long TenantId, long FormId, long PropertyId)
    : ICommand<Result<string>>;

internal sealed class DeletePropertyHandler(
    IPersonalizationDbContext db,
    IRepository<Form> forms)
    : ICommandHandler<DeletePropertyCommand, Result<string>>
{
    public async Task<Result<string>> Handle(
        DeletePropertyCommand request,
        CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<string>();
        }

        Result<Property> loaded = await LoadAsync(request, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.ToErrorResult<string>();
        }

        await SoftDeleteAsync(loaded.Value!, cancellationToken);
        return Result.Success(loaded.Value!.Id.ToString());
    }

    private async Task<Result<Property>> LoadAsync(
        DeletePropertyCommand request,
        CancellationToken cancellationToken)
    {
        Property? property = await db.Properties.FirstOrDefaultAsync(
            row => row.Id == request.PropertyId && row.FormId == request.FormId,
            cancellationToken);
        return property is null
            ? Result.NotFound("Audience property not found.")
            : Result.Success(property);
    }

    private async Task SoftDeleteAsync(Property property, CancellationToken cancellationToken)
    {
        List<PropertyValue> values = await db.PropertyValues
            .Where(value => value.PropertyId == property.Id)
            .ToListAsync(cancellationToken);

        foreach (PropertyValue value in values)
        {
            value.Delete();
        }

        property.Delete();
        await db.SaveChangesAsync(cancellationToken);
    }
}
