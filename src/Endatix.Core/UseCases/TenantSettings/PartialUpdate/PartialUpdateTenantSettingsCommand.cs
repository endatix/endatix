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

    /// <summary>
    /// When set, updates the default submission session token TTL in hours.
    /// Ignored when <see cref="ClearSubmissionTokenExpiryHours"/> is true.
    /// </summary>
    public int? SubmissionTokenExpiryHours { get; init; }

    /// <summary>
    /// When true, clears the tenant session TTL so tokens never expire
    /// (forms without a form-level override inherit never-expire).
    /// </summary>
    public bool ClearSubmissionTokenExpiryHours { get; init; }
}
