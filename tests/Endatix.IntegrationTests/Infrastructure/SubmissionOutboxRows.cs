using System.Text.Json;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.Infrastructure;

/// <summary>The outbox rows one submission's events were captured to, in capture order.</summary>
internal static class SubmissionOutboxRows
{
    public static async Task<IReadOnlyList<SubmissionOutboxRow>> ReadAsync(
        IServiceProvider services,
        long submissionId,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Payload is jsonb on PostgreSQL, so match the submission id in memory.
        var rows = await db.OutboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderBy(row => row.Id)
            .Select(row => new { row.EventType, row.Payload })
            .ToListAsync(cancellationToken);
        return
        [
            .. rows
                .Select(row => (row.EventType, Payload: JsonDocument.Parse(row.Payload).RootElement))
                .Where(row => row.Payload.TryGetProperty("submissionId", out var id) && id.GetString() == submissionId.ToString())
                .Select(row => new SubmissionOutboxRow(
                    row.EventType,
                    row.Payload.TryGetProperty("collectionStatus", out var status) ? status.GetProperty("code").GetString() : null,
                    LongOf(row.Payload.GetProperty("revision")),
                    row.Payload.GetProperty("isTestSubmission").GetBoolean())),
        ];
    }

    // Longs go on the wire as strings (LongToStringConverter).
    private static long LongOf(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? long.Parse(value.GetString()!) : value.GetInt64();
}

internal sealed record SubmissionOutboxRow(string EventType, string? CollectionStatus, long Revision, bool IsTestSubmission);
