using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Audience.Contracts;

namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// One person per tenant + normalized identifier. Not a profile store.
/// </summary>
public sealed class Member : BaseEntity, IAggregateRoot, ITenantOwned
{
    private Member()
    {
        Identifier = string.Empty;
    }

    public Member(MemberCreateArgs args)
    {
        Guard.Against.Null(args);
        Guard.Against.NegativeOrZero(args.TenantId);
        Guard.Against.NullOrWhiteSpace(args.Identifier);
        Guard.Against.Null(args.Normalizer);
        if (!AudienceIdentifierKindCodes.IsKnown(args.IdentifierKind))
        {
            throw new ArgumentException(
                $"Unknown identifier kind '{args.IdentifierKind}'.",
                nameof(args));
        }

        TenantId = args.TenantId;
        Identifier = Normalize(args.Identifier, args.IdentifierKind, args.Normalizer);
    }

    public long TenantId { get; private set; }

    public string Identifier { get; private set; }

    public long? SubmitterId { get; private set; }

    public void BindSubmitter(long submitterId)
    {
        Guard.Against.NegativeOrZero(submitterId);
        SubmitterId = submitterId;
    }

    /// <summary>
    /// Emails use <see cref="IValueNormalizer"/>, the same rule as app users.
    /// External ids keep their case, because source systems treat <c>AbC</c> and <c>abc</c>
    /// as different records.
    /// </summary>
    public static string Normalize(
        string identifier,
        string identifierKind,
        IValueNormalizer normalizer)
    {
        Guard.Against.Null(normalizer);
        string trimmed = identifier.Trim();
        if (identifierKind != AudienceIdentifierKindCodes.Email)
        {
            return trimmed;
        }

        return normalizer.Normalize(trimmed) ?? trimmed;
    }
}

/// <summary>
/// Inputs for creating a <see cref="Member"/>.
/// </summary>
public sealed record MemberCreateArgs(
    long TenantId,
    string Identifier,
    string IdentifierKind,
    IValueNormalizer Normalizer);
