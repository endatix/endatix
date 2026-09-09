using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.ExportFormats;

/// <summary>
/// Native defaults every tenant should have. Shared by <c>SeedDefaultsAsync</c>
/// (outbox <c>tenant.created</c>) and PostgreSQL data migrations.
/// <c>IdSuffix</c> is the <c>hashtextextended</c> salt — keep stable.
/// </summary>
public sealed record DefaultExportFormat(
    string Name,
    ExportTarget Target,
    ExportDeliveryFormat Delivery,
    string Description,
    string SettingsJson,
    string IdSuffix);

public static class DefaultExportFormats
{
    public const string MappingIdSuffix = "default-map";

    private static readonly string SubmissionsSettingsJson = JsonSerializer.Serialize(new
    {
        aliasProfile = "native",
        keySeparator = "__",
        includeTestSubmissions = false,
    });

    private static readonly string CodebookSettingsJson = JsonSerializer.Serialize(new
    {
        aliasProfile = "native",
        keySeparator = "__",
    });

    public static readonly DefaultExportFormat Csv = new(
        "CSV",
        ExportTarget.Submissions,
        ExportDeliveryFormat.Csv,
        "Default CSV export for form submissions",
        SubmissionsSettingsJson,
        "csv");

    public static readonly DefaultExportFormat Json = new(
        "JSON",
        ExportTarget.Submissions,
        ExportDeliveryFormat.Json,
        "Default JSON export for form submissions",
        SubmissionsSettingsJson,
        "json");

    public static readonly DefaultExportFormat Xlsx = new(
        "Excel (XLSX)",
        ExportTarget.Submissions,
        ExportDeliveryFormat.Xlsx,
        "Default Excel export for form submissions",
        SubmissionsSettingsJson,
        "xlsx");

    public static readonly DefaultExportFormat Codebook = new(
        "Codebook",
        ExportTarget.Codebook,
        ExportDeliveryFormat.Json,
        "Default form definition codebook export",
        CodebookSettingsJson,
        "codebook");

    /// <summary>Tenant default mapping points at Native CSV.</summary>
    public static DefaultExportFormat TenantDefault => Csv;

    public static IReadOnlyList<DefaultExportFormat> All { get; } = [Csv, Json, Xlsx, Codebook];
}
