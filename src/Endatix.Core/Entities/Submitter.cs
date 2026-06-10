using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents a submitter of a submission.
/// </summary>
public sealed class Submitter : TenantEntity, IAggregateRoot
{
    private Submitter() { } // For EF Core

    private Submitter(
        long tenantId,
        string authProvider,
        string? externalSubjectId,
        string? displayId,
        long? appUserId,
        string? profileJson,
        DateTimeOffset lastSeenAt)
        : base(tenantId)
    {
        Guard.Against.NullOrWhiteSpace(authProvider);

        AuthProvider = authProvider;
        ExternalSubjectId = NormalizeOptional(externalSubjectId);
        DisplayId = NormalizeOptional(displayId);
        AppUserId = appUserId;
        ProfileJson = NormalizeOptional(profileJson);
        LastSeenAt = lastSeenAt.UtcDateTime;
    }

    public string AuthProvider { get; private set; } = null!;
    public string? ExternalSubjectId { get; private set; }
    public string? DisplayId { get; private set; }
    public long? AppUserId { get; private set; }
    public string? ProfileJson { get; private set; }
    public DateTime LastSeenAt { get; private set; }

    public static Submitter Create(
        long tenantId,
        string authProvider,
        string? externalSubjectId,
        string? displayId,
        long? appUserId,
        string? profileJson,
        DateTimeOffset lastSeenAt) =>
        new(
            tenantId,
            authProvider,
            externalSubjectId,
            displayId,
            appUserId,
            profileJson,
            lastSeenAt);

    public void Refresh(string? displayId, string? profileJson, DateTimeOffset lastSeenAt)
    {
        if (!string.IsNullOrWhiteSpace(displayId))
        {
            DisplayId = displayId;
        }

        if (!string.IsNullOrWhiteSpace(profileJson))
        {
            ProfileJson = profileJson;
        }

        LastSeenAt = lastSeenAt.UtcDateTime;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
