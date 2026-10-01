using System.ComponentModel.DataAnnotations.Schema;
using Ardalis.GuardClauses;
using Endatix.Modules.Reporting.Contracts;

namespace Endatix.Modules.Reporting.Domain;

/// <summary>
/// Reporting pipeline sync state for a submission (flatten → read model → export).
/// Distinct from core <see cref="Endatix.Core.Entities.SubmissionStatus"/> (tenant business workflow).
/// </summary>
[ComplexType]
public sealed class SubmissionIntegrationState
{
    public const int CodeMaxLength = SubmissionIntegrationStatusCodes.MaxLength;
    public const int MaxErrorLength = 2000;

    private SubmissionIntegrationState() { }

    public string Code { get; private set; } = SubmissionIntegrationStatusCodes.Pending;

    public DateTime? LastAttemptAt { get; private set; }

    public DateTime? ProcessedAt { get; private set; }

    public string? LastError { get; private set; }

    public static SubmissionIntegrationState CreatePending(DateTime attemptedAt) =>
        new()
        {
            Code = SubmissionIntegrationStatusCodes.Pending,
            LastAttemptAt = attemptedAt,
        };

    public bool IsTerminal =>
        Code is SubmissionIntegrationStatusCodes.Processed
            or SubmissionIntegrationStatusCodes.Failed
            or SubmissionIntegrationStatusCodes.Skipped;

    public bool IsExportable => Code == SubmissionIntegrationStatusCodes.Processed;

    public static SubmissionIntegrationState FromCode(string code)
    {
        Guard.Against.NullOrWhiteSpace(code);

        var normalized = code.ToLowerInvariant();
        if (!SubmissionIntegrationStatusCodes.IsKnown(normalized))
        {
            throw new ArgumentException($"Invalid integration status code: {code}", nameof(code));
        }

        return new SubmissionIntegrationState { Code = normalized };
    }

    public SubmissionIntegrationSnapshotDto ToSnapshot() =>
        new(Code, ProcessedAt, LastAttemptAt, LastError);

    internal static string? TruncateError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return null;
        }

        return error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
    }
}
