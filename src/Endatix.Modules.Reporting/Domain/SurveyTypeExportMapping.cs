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
        Guard.Against.NegativeOrZero(tenantId, nameof(tenantId));
        Guard.Against.NegativeOrZero(exportFormatId, nameof(exportFormatId));

        TenantId = tenantId;
        ExportFormatId = exportFormatId;
        SurveyTypeId = surveyTypeId;
    }

    public long TenantId { get; private set; }

    /// <summary>
    /// Nullable until survey types persistence is available.
    /// </summary>
    public long? SurveyTypeId { get; private set; }

    public long ExportFormatId { get; private set; }

    public ExportFormat ExportFormat { get; private set; } = null!;
}
