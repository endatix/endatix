using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Branding;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Entities;

/// <summary>
/// A tenant's own brand (logo, icon, accent), one per tenant. It keeps the published override and the one before it,
/// so a rollback is one step and a second rollback undoes the first.
/// </summary>
/// <remarks>
/// <see cref="Revision"/> counts the changes to the published override and is the optimistic concurrency token: a write
/// names the revision it read and is refused when another write came first. A publish of the value already published
/// changes nothing, whatever revision it names, so a retried request leaves the previous version in place.
/// </remarks>
public sealed class TenantBranding : IAggregateRoot, ITenantOwned
{
    private TenantBranding() { } // For EF Core

    /// <summary>Starts a tenant's branding with nothing published, at revision 0.</summary>
    public TenantBranding(long tenantId)
    {
        Guard.Against.NegativeOrZero(tenantId);
        TenantId = tenantId;
    }

    /// <summary>The tenant, and the primary key.</summary>
    public long TenantId { get; private set; }

    /// <summary>The override the tenant shows, or null when the deployment brand applies.</summary>
    public string? PublishedJson { get; private set; }

    /// <summary>The override published before <see cref="PublishedJson"/>: what a rollback returns to.</summary>
    public string? PreviousJson { get; private set; }

    /// <summary>An unpublished override. Reserved for draft and preview editing; nothing writes it yet.</summary>
    public string? DraftJson { get; }

    /// <summary>Goes up by one on every change to the published override. 0 until the first publish.</summary>
    public long Revision { get; private set; }

    /// <summary>When the published override last changed, by a publish or a rollback.</summary>
    public DateTime? PublishedAt { get; private set; }

    /// <summary>
    /// Publishes <paramref name="value"/> and keeps the current override as the previous one.
    /// </summary>
    /// <param name="value">The validated override, for this tenant.</param>
    /// <param name="expectedRevision">The <see cref="Revision"/> the caller read.</param>
    /// <param name="now">The time of the publish.</param>
    /// <returns>Success, also when the value is already published (nothing changes); <see cref="ResultStatus.Conflict"/> when
    /// <paramref name="expectedRevision"/> is stale.</returns>
    public Result Publish(BrandOverride value, long expectedRevision, DateTime now)
    {
        EnsureOwnTenant(value);
        if (value.IsSameAs(PublishedJson))
        {
            return Result.Success();
        }

        if (expectedRevision != Revision)
        {
            return StaleRevision();
        }

        PreviousJson = PublishedJson;
        PublishedJson = value.CanonicalJson;
        Changed(now);
        return Result.Success();
    }

    /// <summary>
    /// Swaps the published and previous overrides. After the first publish the previous override is none, so a rollback
    /// returns the tenant to the deployment brand.
    /// </summary>
    /// <param name="expectedRevision">The <see cref="Revision"/> the caller read, so a retried rollback is refused instead of undoing itself.</param>
    /// <param name="now">The time of the rollback.</param>
    /// <returns>Success; <see cref="ResultStatus.Conflict"/> when <paramref name="expectedRevision"/> is stale;
    /// <see cref="ResultStatus.Invalid"/> (<see cref="BrandOverrideProblems.NO_PREVIOUS_VERSION"/>) when nothing was ever published.</returns>
    public Result Rollback(long expectedRevision, DateTime now)
    {
        if (expectedRevision != Revision)
        {
            return StaleRevision();
        }

        if (PublishedJson is null && PreviousJson is null)
        {
            return Result.Invalid(new ValidationError(
                "",
                "There is no previous branding to return to.",
                BrandOverrideProblems.NO_PREVIOUS_VERSION,
                ValidationSeverity.Error));
        }

        (PublishedJson, PreviousJson) = (PreviousJson, PublishedJson);
        Changed(now);
        return Result.Success();
    }

    private void EnsureOwnTenant(BrandOverride value)
    {
        Guard.Against.Null(value);
        if (value.TenantId != TenantId)
        {
            throw new ArgumentException("The override was checked for another tenant.", nameof(value));
        }
    }

    private void Changed(DateTime now)
    {
        Revision++;
        PublishedAt = now;
    }

    private Result StaleRevision() =>
        Result.Conflict($"The branding changed since it was read; it is now at revision {Revision}.");
}
