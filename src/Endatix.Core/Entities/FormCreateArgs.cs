namespace Endatix.Core.Entities;

/// <summary>
/// Unified creation arguments for <see cref="Form"/>. Named properties avoid positional
/// reordering hazards as optional form settings grow.
/// </summary>
public sealed record FormCreateArgs(
    long TenantId,
    string Name,
    string? Description = null,
    bool IsEnabled = false,
    bool IsPublic = true,
    bool LimitOnePerUser = false,
    string? Metadata = null,
    string? WebHookSettingsJson = null,
    long? FolderId = null,
    int? SubmissionTokenExpiryHours = null);
