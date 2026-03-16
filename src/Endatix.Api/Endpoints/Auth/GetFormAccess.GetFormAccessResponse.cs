using Endatix.Core.Abstractions.Data;
using Endatix.Core.Authorization.Models;
using Endatix.Core.Infrastructure.Caching;

namespace Endatix.Api.Endpoints.Auth;

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
    /// <summary>
    /// Creates a new instance of the <see cref="GetFormAccessResponse"/> from a cached <see cref="SubmissionAccessData"/>.
    /// </summary>
    /// <param name="cached">The cached <see cref="SubmissionAccessData"/>.</param>
    /// <returns>The <see cref="GetFormAccessResponse"/>.</returns>
    public static GetFormAccessResponse FromCached(Cached<SubmissionAccessData> cached)
        => new(cached.Data.FormId, cached.Data.SubmissionId, cached.Data.FormPermissions, cached.Data.SubmissionPermissions, cached.CachedAt, cached.ExpiresAt, cached.ETag);
}
