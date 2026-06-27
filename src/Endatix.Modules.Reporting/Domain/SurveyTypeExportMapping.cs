using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Reporting.Domain;

/// <summary>
/// Links a survey type (or tenant fallback scope) to an allowed <see cref="ExportFormat"/>.
/// Multiple formats may be configured per survey type; <see cref="IsDefault"/> marks the pre-selected option.
/// Duplicate format links per scope are enforced in application code
/// </summary>
public sealed class SurveyTypeExportMapping : BaseEntity, ITenantOwned, IAggregateRoot
{
    private SurveyTypeExportMapping() { }

    public SurveyTypeExportMapping(
        long tenantId,
        long exportFormatId,
        long? surveyTypeId = null,
        bool isDefault = false)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NegativeOrZero(exportFormatId);

        TenantId = tenantId;
        ExportFormatId = exportFormatId;
        SurveyTypeId = surveyTypeId;
        IsDefault = isDefault;
    }

    public long TenantId { get; private set; }

    /// <summary>
    /// Survey type when set; <see langword="null"/> is the tenant-wide fallback scope.
    /// </summary>
    public long? SurveyTypeId { get; private set; }

    public long ExportFormatId { get; private set; }

    /// <summary>
    /// When <see langword="true"/>, this format is the default for its scope
    /// (per survey type, or tenant fallback when <see cref="SurveyTypeId"/> is null).
    /// </summary>
    public bool IsDefault { get; private set; }

    public ExportFormat ExportFormat { get; private set; } = null!;

    public void MarkAsDefault() => IsDefault = true;

    public void ClearDefault() => IsDefault = false;
}
