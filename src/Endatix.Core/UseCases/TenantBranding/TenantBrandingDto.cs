namespace Endatix.Core.UseCases.TenantBranding;

/// <summary>A tenant's branding after a write.</summary>
/// <param name="PublishedJson">The published override, or null when the deployment brand applies.</param>
/// <param name="Revision">The revision the next write names.</param>
/// <param name="PublishedAt">When the published override last changed.</param>
public sealed record TenantBrandingDto(string? PublishedJson, long Revision, DateTime? PublishedAt)
{
    internal static TenantBrandingDto From(Entities.TenantBranding branding) =>
        new(branding.PublishedJson, branding.Revision, branding.PublishedAt);
}
