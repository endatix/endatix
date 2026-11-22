using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Get;
using Endatix.Core.UseCases.TenantBranding.History;

namespace Endatix.Api.Endpoints.TenantBranding;

/// <summary>A tenant's branding, as the admin read and every write return it.</summary>
/// <param name="Entitled">Whether the tenant's plan includes branding. When false, <paramref name="Published"/> is null.</param>
/// <param name="Published">The live brand override as a JSON object; null when the deployment brand applies.</param>
/// <param name="PublishedNumber">The live version's number; null when nothing is published.</param>
/// <param name="Version">The version the next write names in <c>If-Match</c>; also the response's <c>ETag</c>.</param>
/// <param name="PublishedAt">When the live version was published.</param>
[Experimental("ENDATIX0002")]
public sealed record TenantBrandingModel(
    bool Entitled,
    JsonElement? Published,
    long? PublishedNumber,
    long Version,
    DateTime? PublishedAt)
{
    internal static TenantBrandingModel Map(TenantBrandingReadDto read) => From(read.Entitled, read.Branding);

    // Only an entitled tenant can write, so a write's result is always entitled.
    internal static TenantBrandingModel MapWrite(TenantBrandingDto branding) => From(true, branding);

    private static TenantBrandingModel From(bool entitled, TenantBrandingDto branding) => new(
        entitled,
        BrandingJson.ToObject(branding.PublishedJson),
        branding.PublishedNumber,
        branding.Version,
        branding.PublishedAt);
}

/// <summary>One version in a tenant's branding history. Restore names its <paramref name="Number"/>.</summary>
/// <param name="Number">The version's number, 1..n per tenant.</param>
/// <param name="CreatedAt">When it was published.</param>
/// <param name="CreatedBy">The user who published it; null when not known.</param>
/// <param name="Source"><c>publish</c> or <c>restore</c>.</param>
/// <param name="RestoredFromNumber">For a restore, the version it copies; otherwise null.</param>
[Experimental("ENDATIX0002")]
public sealed record TenantBrandingVersionModel(
    long Number,
    DateTime CreatedAt,
    long? CreatedBy,
    string Source,
    long? RestoredFromNumber)
{
    internal static TenantBrandingVersionModel Map(TenantBrandingVersionDto version) => new(
        version.Number,
        version.CreatedAt,
        version.CreatedBy,
        version.Source,
        version.RestoredFromNumber);
}

/// <summary>One problem with a branding write: where (a JSON Pointer), which rule (a stable code), and why.</summary>
/// <param name="Path">The JSON Pointer into the override; empty for the request as a whole.</param>
/// <param name="Code">The stable problem code, such as <c>unknown_version</c> or <c>invalid_json</c>.</param>
/// <param name="Detail">A human-readable explanation.</param>
[Experimental("ENDATIX0002")]
public sealed record BrandingProblem(string Path, string Code, string Detail);

/// <summary>Stored brand JSON on the wire: an object, never a string.</summary>
internal static class BrandingJson
{
    internal static JsonElement? ToObject(string? json)
    {
        if (json is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
