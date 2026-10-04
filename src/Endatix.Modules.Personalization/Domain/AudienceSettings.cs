using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Personalization.Contracts;

namespace Endatix.Modules.Personalization.Domain;

/// <summary>
/// One row per tenant: which field identifies a person across forms.
/// </summary>
public sealed class AudienceSettings : BaseEntity, IAggregateRoot, ITenantOwned
{
    private AudienceSettings() { }

    public AudienceSettings(long tenantId, string identifierKind = AudienceIdentifierKindCodes.Email)
    {
        Guard.Against.NegativeOrZero(tenantId);
        if (!AudienceIdentifierKindCodes.IsKnown(identifierKind))
        {
            throw new ArgumentException($"Unknown identifier kind '{identifierKind}'.", nameof(identifierKind));
        }

        TenantId = tenantId;
        IdentifierKind = identifierKind;
    }

    public long TenantId { get; private set; }

    public string IdentifierKind { get; private set; } = null!;

    /// <summary>
    /// Changes the match key. Caller must ensure no <see cref="Member"/> exists yet.
    /// </summary>
    public void SetIdentifierKind(string identifierKind)
    {
        if (!AudienceIdentifierKindCodes.IsKnown(identifierKind))
        {
            throw new ArgumentException($"Unknown identifier kind '{identifierKind}'.", nameof(identifierKind));
        }

        IdentifierKind = identifierKind;
    }
}
