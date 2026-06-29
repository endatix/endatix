namespace Endatix.Modules.Reporting.Contracts;

/// <summary>
/// Public integration-status codes for reporting API filters and DTOs.
/// Domain semantics live in <c>Endatix.Modules.Reporting.Domain.SubmissionIntegrationState</c>.
/// </summary>
public static class SubmissionIntegrationStatusCodes
{
    public const int MaxLength = 32;

    public const string NotProcessed = "not_processed";
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Processed = "processed";
    public const string Failed = "failed";
    public const string Skipped = "skipped";

    public static bool IsKnown(string code) =>
        code is NotProcessed or Pending or Processing or Processed or Failed or Skipped;
}
