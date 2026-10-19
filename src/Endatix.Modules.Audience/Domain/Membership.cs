using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// Places an <see cref="Member"/> on one form. Deleting this row removes the person from
/// that form only. A test person (<see cref="IsTest"/>) is never issued a personalised link.
/// </summary>
public sealed class Membership : BaseEntity, IAggregateRoot, ITenantOwned
{
    /// <summary>
    /// Database names of the unique indexes on <see cref="Membership"/>.
    /// </summary>
    public static class UniqueConstraints
    {
        public const string MemberPerForm = "IX_Memberships_Member";
    }

    private Membership() { }

    public Membership(MembershipCreateArgs args)
    {
        Guard.Against.Null(args);
        Guard.Against.NegativeOrZero(args.TenantId);
        Guard.Against.NegativeOrZero(args.FormId);
        Guard.Against.NegativeOrZero(args.MemberId);

        TenantId = args.TenantId;
        FormId = args.FormId;
        MemberId = args.MemberId;
        IsTest = args.IsTest;
    }

    public long TenantId { get; private set; }

    public long FormId { get; private set; }

    public long MemberId { get; private set; }

    /// <summary>
    /// Set when the person is added and never changed: a person exists on this form only for testing.
    /// </summary>
    public bool IsTest { get; private set; }
}
