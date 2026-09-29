using System.Text.Json;
using System.Text.Json.Serialization;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Which submissions a backfill batch pages. Omitted on the wire means <see cref="Completed"/>.
/// Wire: <c>completed</c> | <c>incomplete</c>.
/// </summary>
[JsonConverter(typeof(SubmissionBackfillCompletionJsonConverter))]
public enum SubmissionBackfillCompletion
{
    Completed = 0,
    Incomplete = 1,
}

/// <summary>
/// Serializes <see cref="SubmissionBackfillCompletion"/> as camelCase wire strings.
/// </summary>
public sealed class SubmissionBackfillCompletionJsonConverter : JsonStringEnumConverter
{
    public SubmissionBackfillCompletionJsonConverter()
        : base(JsonNamingPolicy.CamelCase)
    {
    }
}
