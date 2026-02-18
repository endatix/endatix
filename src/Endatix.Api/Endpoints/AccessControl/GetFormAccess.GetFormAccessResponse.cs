using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Infrastructure.Caching;

namespace Endatix.Api.Endpoints.AccessControl;

public record GetFormAccessResponse(
    string FormId,
    string? SubmissionId,
    HashSet<string> FormPermissions,
    HashSet<string> SubmissionPermissions,
    DateTime CachedAt,
    DateTime ExpiresAt,
    string ETag
) : ICachedData
{
    public static GetFormAccessResponse FromCached(Cached<SubmissionAccessData> cached)
        => new(cached.Data.FormId, cached.Data.SubmissionId, cached.Data.FormPermissions, cached.Data.SubmissionPermissions, cached.CachedAt, cached.ExpiresAt, cached.ETag);
}
