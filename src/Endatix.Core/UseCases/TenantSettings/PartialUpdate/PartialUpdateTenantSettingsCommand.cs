using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantSettings.PartialUpdate;

/// <summary>
/// Partially updates tenant-scoped settings for the current tenant.
/// </summary>
public sealed record PartialUpdateTenantSettingsCommand : ICommand<Result<TenantSettingsDto>>
{
    /// <summary>
    /// When set, updates whether forms and templates must be assigned to a folder.
    /// </summary>
    public bool? RequireFolderAssignment { get; init; }
}
