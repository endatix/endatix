using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Personalization.Contracts;

namespace Endatix.Modules.Personalization.Domain;

/// <summary>
/// One person per tenant + normalized identifier. Not a profile store.
/// </summary>
public sealed class Member : BaseEntity, IAggregateRoot, ITenantOwned
{
    private Member() { }

    public Member(long tenantId, string identifier, string identifierKind)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NullOrWhiteSpace(identifier);

        TenantId = tenantId;
        Identifier = Normalize(identifier, identifierKind);
    }

    public long TenantId { get; private set; }

    public string Identifier { get; private set; } = null!;

    public long? SubmitterId { get; private set; }

    public void BindSubmitter(long submitterId)
    {
        Guard.Against.NegativeOrZero(submitterId);
        SubmitterId = submitterId;
    }

    /// <summary>
    /// Emails match case-insensitively. External ids keep their case, because source systems
    /// such as CRMs treat <c>AbC</c> and <c>abc</c> as different records.
    /// </summary>
    public static string Normalize(string identifier, string identifierKind)
    {
        string trimmed = identifier.Trim();
        return identifierKind == AudienceIdentifierKindCodes.Email
            ? trimmed.ToLowerInvariant()
            : trimmed;
    }
}
