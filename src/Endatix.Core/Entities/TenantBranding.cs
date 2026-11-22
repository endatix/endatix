using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Branding;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;

namespace Endatix.Core.Entities;

/// <summary>
/// A tenant's own brand (logo, icon, accent), one per tenant: which <see cref="TenantBrandingVersion"/> is live, and a
/// copy of its JSON so the read is one key lookup.
/// </summary>
/// <remarks>
/// Every change appends a version and makes it live, so the live version is always the newest. A rollback restores the
/// version before the live one as a new version: two rollbacks toggle, and history never rewinds. <see cref="Version"/>
/// is the optimistic concurrency token: a write names the version it read and is refused when another write came
/// first. Publishing (or restoring) the content already live changes nothing, whatever version it names, so a retried
/// request adds no version. A reset is a version too: it shows the deployment brand again (<see cref="PublishedJson"/>
/// null) while <see cref="PublishedNumber"/> points at the reset, so a rollback brings the brand before it back.
/// </remarks>
[Experimental("ENDATIX0002")]
public sealed class TenantBranding : IAggregateRoot, ITenantOwned, IHasVersion
{
    /// <summary>The <see cref="Version"/> a write names when the tenant has no branding row yet.</summary>
    public const long UNSAVED_VERSION = 0;

    /// <summary>
    /// The <see cref="TenantBrandingVersion.Json"/> of a reset: no override, so the deployment brand applies. No publish
    /// can store it, since an override names its <c>version</c>.
    /// </summary>
    public const string DEPLOYMENT_BRAND_JSON = "{}";

    private static readonly string _deploymentBrandHash = CanonicalContentHash.Of(DEPLOYMENT_BRAND_JSON);

    private TenantBranding() { } // For EF Core

    /// <summary>Starts a tenant's branding with nothing published, at <see cref="UNSAVED_VERSION"/>.</summary>
    public TenantBranding(long tenantId)
    {
        Guard.Against.NegativeOrZero(tenantId);
        TenantId = tenantId;
    }

    /// <summary>The tenant, and the primary key.</summary>
    public long TenantId { get; private set; }

    /// <summary>Goes up by one on every change. 1 after the first publish; <see cref="UNSAVED_VERSION"/> before it.</summary>
    public long Version { get; private set; } = UNSAVED_VERSION;

    /// <summary>The live version's <see cref="TenantBrandingVersion.Number"/>, or null when nothing was published yet.
    /// After a reset it is the reset's number.</summary>
    public long? PublishedNumber { get; private set; }

    /// <summary>The live version's JSON, or null when the deployment brand applies.</summary>
    public string? PublishedJson { get; private set; }

    /// <summary>When the live version was published, by a publish, a restore or a reset.</summary>
    public DateTime? PublishedAt { get; private set; }

    /// <summary>Publishes <paramref name="value"/> as a new version.</summary>
    /// <param name="value">The validated override, for this tenant.</param>
    /// <param name="expectedVersion">The <see cref="Version"/> the caller read.</param>
    /// <param name="stamp">When, and by whom.</param>
    /// <returns>The version to save; null when the value is already live (nothing changes);
    /// <see cref="ResultStatus.Conflict"/> when <paramref name="expectedVersion"/> is stale.</returns>
    internal Result<TenantBrandingVersion?> Publish(BrandOverride value, long expectedVersion, ContentVersionStamp stamp)
    {
        Guard.Against.Null(value);
        EnsureOwnTenant(value.TenantId);
        if (IsLive(value.ContentHash))
        {
            return Result<TenantBrandingVersion?>.Success(null);
        }

        return expectedVersion != Version
            ? StaleVersion()
            : Append(new(value.CanonicalJson, value.ContentHash, ContentVersionSources.PUBLISH, null), stamp);
    }

    /// <summary>Shows the deployment brand again, as a new version (<see cref="ContentVersionSources.RESET"/>).</summary>
    /// <param name="expectedVersion">The <see cref="Version"/> the caller read.</param>
    /// <param name="stamp">When, and by whom.</param>
    /// <returns>The version to save; null when the deployment brand already applies (nothing changes);
    /// <see cref="ResultStatus.Conflict"/> when <paramref name="expectedVersion"/> is stale.</returns>
    internal Result<TenantBrandingVersion?> Reset(long expectedVersion, ContentVersionStamp stamp)
    {
        if (IsLive(_deploymentBrandHash))
        {
            return Result<TenantBrandingVersion?>.Success(null);
        }

        return expectedVersion != Version
            ? StaleVersion()
            : Append(new(DEPLOYMENT_BRAND_JSON, _deploymentBrandHash, ContentVersionSources.RESET, null), stamp);
    }

    /// <summary>
    /// The number a restore publishes again: <paramref name="number"/>, or for a rollback (null) the version before the
    /// live one.
    /// </summary>
    /// <remarks>It does not check the caller's version: <see cref="Restore"/> does, after it has seen whether the
    /// target's content is already live.</remarks>
    /// <returns>The number; <see cref="ResultStatus.Invalid"/> when there is no such version
    /// (<see cref="BrandOverrideProblems.UNKNOWN_VERSION"/>) or nothing before the live one
    /// (<see cref="BrandOverrideProblems.NO_PREVIOUS_VERSION"/>).</returns>
    internal Result<long> RestoreTarget(long? number)
    {
        var live = PublishedNumber ?? 0;
        if (number is null)
        {
            return live > 1
                ? live - 1
                : Refused(BrandOverrideProblems.NO_PREVIOUS_VERSION, "There is no previous branding to return to.");
        }

        return number >= 1 && number <= live
            ? number.Value
            : Refused(BrandOverrideProblems.UNKNOWN_VERSION, $"There is no branding version {number}.");
    }

    /// <summary>Publishes a copy of <paramref name="source"/> as a new version; a copy of a reset shows the deployment brand.</summary>
    /// <param name="source">An earlier version of this tenant's branding.</param>
    /// <param name="expectedVersion">The <see cref="Version"/> the caller read, so a retried rollback is refused instead
    /// of undoing itself. Not checked when <paramref name="source"/>'s content is already live.</param>
    /// <param name="stamp">When, and by whom.</param>
    /// <returns>The version to save; null when <paramref name="source"/>'s content is already live;
    /// <see cref="ResultStatus.Conflict"/> when <paramref name="expectedVersion"/> is stale.</returns>
    internal Result<TenantBrandingVersion?> Restore(TenantBrandingVersion source, long expectedVersion, ContentVersionStamp stamp)
    {
        Guard.Against.Null(source);
        EnsureOwnTenant(source.TenantId);
        if (IsLive(source.ContentHash))
        {
            return Result<TenantBrandingVersion?>.Success(null);
        }

        return expectedVersion != Version
            ? StaleVersion()
            : Append(new(CanonicalBrandJson.Write(source.Json), source.ContentHash, ContentVersionSources.RESTORE, source.Number), stamp);
    }

    // The cache may come back with its keys reordered (jsonb), so its hash is taken in canonical form. With no cache the
    // deployment brand is live, before the first publish as after a reset.
    private bool IsLive(string contentHash) => contentHash ==
        (PublishedJson is null ? _deploymentBrandHash : CanonicalContentHash.Of(CanonicalBrandJson.Write(PublishedJson)));

    private TenantBrandingVersion Append(Content content, ContentVersionStamp stamp)
    {
        var version = new TenantBrandingVersion
        {
            TenantId = TenantId,
            Number = (PublishedNumber ?? 0) + 1,
            Json = content.Json,
            ContentHash = content.Hash,
            Source = content.Source,
            RestoredFromNumber = content.RestoredFromNumber,
            CreatedAt = stamp.At,
            CreatedBy = stamp.By,
        };
        PublishedNumber = version.Number;
        PublishedJson = content.Hash == _deploymentBrandHash ? null : version.Json;
        PublishedAt = stamp.At;
        Version++;
        return version;
    }

    private void EnsureOwnTenant(long tenantId)
    {
        if (tenantId != TenantId)
        {
            throw new ArgumentException("The value belongs to another tenant.", nameof(tenantId));
        }
    }

    private Result StaleVersion() =>
        Result.Conflict($"The branding changed since it was read; it is now at version {Version}.");

    private static Result Refused(string code, string detail) =>
        Result.Invalid(new ValidationError("", detail, code, ValidationSeverity.Error));

    private readonly record struct Content(string Json, string Hash, string Source, long? RestoredFromNumber);
}
