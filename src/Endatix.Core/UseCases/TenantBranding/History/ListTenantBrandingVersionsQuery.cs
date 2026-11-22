using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.History;

/// <summary>
/// Lists the current tenant's branding versions, newest first.
/// </summary>
/// <remarks>The <c>WhiteLabeling</c> feature flag is the caller's to check (<c>EndpointFeatureGate</c>).</remarks>
/// <param name="Page">The page, from 1; the first page when null.</param>
/// <param name="PageSize">The page size; the default when null.</param>
[Experimental("ENDATIX0002")]
public sealed record ListTenantBrandingVersionsQuery(int? Page, int? PageSize)
    : IQuery<Result<Paged<TenantBrandingVersionDto>>>;

/// <summary>One branding version in the history list. The JSON is not listed: restore names the number.</summary>
/// <param name="Number">The version's number, 1..n per tenant.</param>
/// <param name="CreatedAt">When it was published.</param>
/// <param name="CreatedBy">The user who published it; null when not known.</param>
/// <param name="Source"><c>publish</c>, <c>restore</c> or <c>reset</c>.</param>
/// <param name="RestoredFromNumber">For a restore, or a reset made by restoring one, the version it copies; otherwise
/// null.</param>
[Experimental("ENDATIX0002")]
public sealed record TenantBrandingVersionDto(
    long Number,
    DateTime CreatedAt,
    long? CreatedBy,
    string Source,
    long? RestoredFromNumber)
{
    internal static TenantBrandingVersionDto From(Entities.TenantBrandingVersion version) =>
        new(version.Number, version.CreatedAt, version.CreatedBy, version.Source, version.RestoredFromNumber);
}
