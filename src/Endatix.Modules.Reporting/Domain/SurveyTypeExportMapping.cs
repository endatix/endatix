using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Reporting.Domain;

/// <summary>
/// Maps a survey type to a default <see cref="ExportFormat"/>.
/// </summary>
public sealed class SurveyTypeExportMapping : BaseEntity, ITenantOwned, IAggregateRoot
{
    private SurveyTypeExportMapping() { }

    public SurveyTypeExportMapping(
        long tenantId,
        long exportFormatId,
        long? surveyTypeId = null)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NegativeOrZero(exportFormatId);

        TenantId = tenantId;
        ExportFormatId = exportFormatId;
        SurveyTypeId = surveyTypeId;
    }

    public long TenantId { get; private set; }

    /// <summary>
    /// Survey type when set; <see langword="null"/> is the tenant-wide default export format (one per tenant).
    /// </summary>
    public long? SurveyTypeId { get; private set; }

    public long ExportFormatId { get; private set; }

    public ExportFormat ExportFormat { get; private set; } = null!;
}
