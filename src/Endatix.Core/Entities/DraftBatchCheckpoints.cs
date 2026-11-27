using System.Text;
using System.Text.Json;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;

namespace Endatix.Core.Entities;

internal sealed record DraftDocument(string Json, string Hash)
{
    internal static DraftDocument From(string json) => new(json, CanonicalSurveyJson.Hash(json));
}

internal sealed class DraftBatchCheckpoints(DraftDocument terminal, bool snapshotHead)
{
    internal DraftDocument Terminal { get; } = terminal;
    internal bool SnapshotHead { get; } = snapshotHead;
    internal Dictionary<int, DraftDocument> Documents { get; } = [];

    internal static Result<DraftBatchCheckpoints> Prepare(DraftJournalBatch batch, long recordsSinceSnapshot)
    {
        var interval = Encoding.UTF8.GetByteCount(batch.JsonData) > 500_000 ? 100 : 200;
        // If the interval shrank under the already committed tail, the previous head is a known boundary.
        var plan = new DraftBatchCheckpoints(DraftDocument.From(batch.JsonData), recordsSinceSnapshot >= interval);
        var next = plan.SnapshotHead ? interval : interval - recordsSinceSnapshot;
        foreach (var checkpoint in batch.Checkpoints)
        {
            if (checkpoint is null || checkpoint.AfterRecord != next || next >= batch.Records.Count)
            {
                return Invalid("Supply exactly the required interior checkpoints, in ascending record order.");
            }
            if (!plan.AddDocument(checkpoint))
            {
                return Invalid($"The checkpoint after record {next} must contain a survey JSON object without duplicate properties.");
            }
            next += interval;
        }
        return plan.Finish(batch.Records.Count, next);
    }

    private Result<DraftBatchCheckpoints> Finish(int count, long next)
    {
        if (next < count)
        {
            return Invalid($"Supply the survey after record {next}.");
        }
        if (next == count)
        {
            Documents.Add(count, Terminal);
        }
        return this;
    }

    private bool AddDocument(DraftCheckpoint checkpoint)
    {
        if (string.IsNullOrWhiteSpace(checkpoint.JsonData))
        {
            return false;
        }
        try
        {
            Documents.Add(checkpoint.AfterRecord, DraftDocument.From(checkpoint.JsonData));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Result<DraftBatchCheckpoints> Invalid(string message) =>
        Result.Invalid(new ValidationError(message) { Identifier = "checkpoints" });
}
