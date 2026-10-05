namespace Endatix.Modules.Audience.Contracts;

/// <summary>
/// Wire codes for the tenant-wide audience match key.
/// </summary>
public static class AudienceIdentifierKindCodes
{
    public const string Email = "email";
    public const string ExternalId = "external_id";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Email,
        ExternalId,
    };

    public static bool IsKnown(string? kind) =>
        kind is not null && All.Contains(kind);
}
