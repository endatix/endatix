using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Personalization.Domain;

/// <summary>
/// One person per tenant + normalized identifier. Not a profile store.
/// </summary>
public sealed class AudienceMember : BaseEntity, IAggregateRoot, ITenantOwned
{
    private AudienceMember() { }

    public AudienceMember(long tenantId, string identifier, long? submitterId = null)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NullOrWhiteSpace(identifier);

        TenantId = tenantId;
        Identifier = Normalize(identifier);
        SubmitterId = submitterId;
    }

    public long TenantId { get; private set; }

    public string Identifier { get; private set; } = null!;

    public long? SubmitterId { get; private set; }

    public void BindSubmitter(long submitterId)
    {
        Guard.Against.NegativeOrZero(submitterId);
        SubmitterId = submitterId;
    }

    public static string Normalize(string identifier) => identifier.Trim().ToLowerInvariant();
}
