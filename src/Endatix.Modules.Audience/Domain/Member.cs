using System.Net.Mail;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Common;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Audience.Contracts;

namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// One person per tenant + match key. Not a profile store.
/// </summary>
public sealed class Member : BaseEntity, IAggregateRoot, ITenantOwned
{
    /// <summary>
    /// Database names of the unique indexes on <see cref="Member"/>.
    /// </summary>
    public static class UniqueConstraints
    {
        public const string IdentifierPerTenant = "IX_Members_Identifier";
    }

    private Member()
    {
        Identifier = string.Empty;
        NormalizedIdentifier = string.Empty;
    }

    public Member(MemberCreateArgs args)
    {
        Guard.Against.Null(args);
        Guard.Against.NegativeOrZero(args.TenantId);
        Guard.Against.Null(args.Normalizer);
        if (!AudienceIdentifierKindCodes.IsKnown(args.IdentifierKind))
        {
            throw new ArgumentException(
                $"Unknown identifier kind '{args.IdentifierKind}'.",
                nameof(args));
        }

        DomainValidationException.ThrowIfError(
            IdentifierError(args.Identifier, args.IdentifierKind),
            nameof(args));

        TenantId = args.TenantId;
        Identifier = args.Identifier.Trim();
        NormalizedIdentifier = Normalize(Identifier, args.IdentifierKind, args.Normalizer);
    }

    public long TenantId { get; private set; }

    /// <summary>The identifier as first entered, trimmed. Shown in the Hub and used for links.</summary>
    public string Identifier { get; private set; }

    /// <summary>The match key. Unique per tenant.</summary>
    public string NormalizedIdentifier { get; private set; }

    public long? SubmitterId { get; private set; }

    public void BindSubmitter(long submitterId)
    {
        Guard.Against.NegativeOrZero(submitterId);
        if (SubmitterId is not null)
        {
            return;
        }

        SubmitterId = submitterId;
    }

    /// <summary>
    /// Rejects a blank or too-long identifier, and an email identifier that is not one address.
    /// </summary>
    public static string? IdentifierError(string? identifier, string identifierKind)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return "Identifier is required.";
        }

        string trimmed = identifier.Trim();
        if (trimmed.Length > DataSchemaConstants.MAX_EMAIL_LENGTH)
        {
            return $"Identifier must be at most {DataSchemaConstants.MAX_EMAIL_LENGTH} characters.";
        }

        return identifierKind == AudienceIdentifierKindCodes.Email && !IsEmailAddress(trimmed)
            ? $"'{trimmed}' is not a valid email address."
            : null;
    }

    /// <summary>
    /// Builds the match key. Emails use <see cref="IValueNormalizer"/>, the same rule as app users.
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

    private static bool IsEmailAddress(string value) =>
        MailAddress.TryCreate(value, out MailAddress? address)
        && string.Equals(address.Address, value, StringComparison.Ordinal);
}

/// <summary>
/// Inputs for creating a <see cref="Member"/>.
/// </summary>
public sealed record MemberCreateArgs(
    long TenantId,
    string Identifier,
    string IdentifierKind,
    IValueNormalizer Normalizer);
