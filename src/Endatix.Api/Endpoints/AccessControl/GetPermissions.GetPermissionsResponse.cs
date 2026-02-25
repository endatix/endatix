using Endatix.Core.Abstractions.Data;
using Endatix.Core.Authorization.Models;
using Endatix.Core.Infrastructure.Caching;

namespace Endatix.Api.Endpoints.AccessControl;

public record GetPermissionsResponse(
    string FormId,
    string? SubmissionId,
    HashSet<string> FormPermissions,
    HashSet<string> SubmissionPermissions,
    DateTime CachedAt,
    DateTime ExpiresAt,
    string ETag
) : ICachedData
{
    public static GetPermissionsResponse FromCached(Cached<SubmissionAccessData> cached)
        => new(cached.Data.FormId, cached.Data.SubmissionId, cached.Data.FormPermissions, cached.Data.SubmissionPermissions, cached.CachedAt, cached.ExpiresAt, cached.ETag);
}
