using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Reporting.Domain;

/// <summary>
/// A saved submissions grid view of a form: column visibility, order and widths, page size and sort.
/// One row per tenant, form and owner. Owner <see cref="SharedOwnerUserId"/> is the view everyone on the form sees.
/// </summary>
public sealed class SubmissionView : BaseEntity, ITenantOwned, IAggregateRoot
{
    /// <summary>
    /// The owner of the shared form view. Not <see langword="null"/>: SQL Server allows one NULL in a unique index
    /// and PostgreSQL any number, and <c>0</c> gives both providers the same rule.
    /// </summary>
    public const long SharedOwnerUserId = 0;

    /// <summary>The revision of a view that was never saved; a save from it creates the view.</summary>
    public const long UnsavedRevision = 0;

    private SubmissionView() { }

    private SubmissionView(SubmissionViewCreateArgs args)
    {
        Guard.Against.Null(args);
        Guard.Against.NegativeOrZero(args.TenantId);
        Guard.Against.NegativeOrZero(args.FormId);
        DomainValidationException.ThrowIfError(SubmissionViewSettings.Error(args.SettingsJson), nameof(args.SettingsJson));

        TenantId = args.TenantId;
        FormId = args.FormId;
        OwnerUserId = SharedOwnerUserId;
        SettingsJson = args.SettingsJson;
        Revision = UnsavedRevision + 1;
        ModifiedBy = args.ModifiedBy;
    }

    public long TenantId { get; private set; }

    public long FormId { get; private set; }

    /// <summary><see cref="SharedOwnerUserId"/> for the form's shared view; a user id for a personal one (later).</summary>
    public long OwnerUserId { get; private set; }

    /// <summary>The settings exactly as saved; see <see cref="SubmissionViewSettings"/> for the shape.</summary>
    public string SettingsJson { get; private set; } = SubmissionViewSettings.DefaultJson;

    /// <summary>
    /// Bumped on every save and checked by the database on update, so a save made from an older read is refused.
    /// </summary>
    public long Revision { get; private set; }

    /// <summary>The user who saved the view last, when known.</summary>
    public long? ModifiedBy { get; private set; }

    /// <summary>Creates the view everyone on the form sees.</summary>
    public static SubmissionView CreateShared(SubmissionViewCreateArgs args) => new(args);

    /// <summary>Replaces the settings and moves to the next revision.</summary>
    public void UpdateSettings(string settingsJson, long? modifiedBy)
    {
        DomainValidationException.ThrowIfError(SubmissionViewSettings.Error(settingsJson), nameof(settingsJson));

        SettingsJson = settingsJson;
        ModifiedBy = modifiedBy;
        Revision++;
    }
}

/// <summary>What a new shared view of a form is made from.</summary>
/// <param name="ModifiedBy">The user saving it, when known.</param>
public sealed record SubmissionViewCreateArgs(long TenantId, long FormId, string SettingsJson, long? ModifiedBy);
