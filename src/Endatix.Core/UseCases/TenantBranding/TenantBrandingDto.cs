using System.Diagnostics.CodeAnalysis;
namespace Endatix.Core.UseCases.TenantBranding;

/// <summary>A tenant's branding after a write.</summary>
/// <param name="PublishedJson">The live version's override, or null when the deployment brand applies.</param>
/// <param name="PublishedNumber">The live version's number, or null when nothing was published. After a reset it is the
/// reset's number, with <paramref name="PublishedJson"/> null.</param>
/// <param name="Version">The version the next write names.</param>
/// <param name="PublishedAt">When the live version was published.</param>
[Experimental("ENDATIX0002")]
public sealed record TenantBrandingDto(string? PublishedJson, long? PublishedNumber, long Version, DateTime? PublishedAt)
{
    internal static TenantBrandingDto From(Entities.TenantBranding branding) =>
        new(branding.PublishedJson, branding.PublishedNumber, branding.Version, branding.PublishedAt);
}
