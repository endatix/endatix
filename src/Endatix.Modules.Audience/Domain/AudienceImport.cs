using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// Summary of one confirmed audience CSV import (sync; not a background job).
/// </summary>
public sealed class AudienceImport : BaseEntity, IAggregateRoot, ITenantOwned
{
    private AudienceImport() { }

    public AudienceImport(AudienceImportCreateArgs args)
    {
        Guard.Against.Null(args);
        Guard.Against.NegativeOrZero(args.TenantId);
        Guard.Against.NegativeOrZero(args.FormId);
        Guard.Against.NullOrWhiteSpace(args.FileName);

        TenantId = args.TenantId;
        FormId = args.FormId;
        FileName = args.FileName.Trim();
        Status = AudienceImportStatusCodes.Completed;
        CreatedCount = args.CreatedCount;
        UpdatedCount = args.UpdatedCount;
        SkippedCount = args.SkippedCount;
        RejectedCount = args.RejectedCount;
        PerformedById = args.PerformedById;
        CompletedAt = DateTime.UtcNow;
    }

    public long TenantId { get; private set; }

    public long FormId { get; private set; }

    public string FileName { get; private set; } = null!;

    public string Status { get; private set; } = null!;

    public int CreatedCount { get; private set; }

    public int UpdatedCount { get; private set; }

    public int SkippedCount { get; private set; }

    public int RejectedCount { get; private set; }

    public string? RejectedRowsBlobKey { get; private set; }

    public long? PerformedById { get; private set; }

    public DateTime CompletedAt { get; private set; }
}

/// <summary>
/// Construction args for <see cref="AudienceImport"/>.
/// </summary>
public sealed record AudienceImportCreateArgs(
    long TenantId,
    long FormId,
    string FileName,
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    int RejectedCount,
    long? PerformedById);

/// <summary>
/// Wire codes for <see cref="AudienceImport.Status"/>.
/// </summary>
public static class AudienceImportStatusCodes
{
    public const string Completed = "completed";
}
