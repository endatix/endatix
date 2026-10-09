using System.Text.Json;
using System.Text.Json.Serialization;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The outcome a finished attempt's handler decided, which could not be written to the row. The trigger that re-fires
/// the job carries it, and that firing writes it, fenced on the attempt that produced it, instead of claiming the row
/// for another attempt: a re-claim would run done work again, or dead-letter a job whose last attempt succeeded.
/// </summary>
/// <remarks>
/// <para>
/// Only the handler's own verdicts are carried. A throw is judged against the attempt budget, which may change while
/// the job waits, so it is left for the re-claim to judge again, as it always was.
/// </para>
/// <para>
/// The outcome is stored in the trigger's job data, so a failure's message is kept by Quartz as well as by the row, and
/// the dashboard shows it to whoever can see the job's triggers. It is the handler's own <c>Result</c> text, never
/// exception text, since a throw is not carried, and at most
/// <see cref="BackgroundJobStateRepository.ErrorMessageMaxLength"/> characters long.
/// </para>
/// </remarks>
/// <param name="Attempt">The attempt that produced the outcome; the write lands only while the row is still on it.</param>
/// <param name="Write">What the outcome writes: <see cref="AttemptRowWrite.Completed"/> or <see cref="AttemptRowWrite.Failed"/>.</param>
/// <param name="ErrorMessage">The handler's failure message, as the row would have recorded it; none for a success.</param>
/// <param name="ClaimedAt">When the attempt was claimed, which its duration is measured from.</param>
/// <param name="EndedAt">When the attempt ended, which the row records as the time it finished.</param>
internal sealed record UnsavedOutcome(
    int Attempt,
    AttemptRowWrite Write,
    string? ErrorMessage,
    DateTime ClaimedAt,
    DateTime EndedAt)
{
    /// <summary>
    /// The trigger data key that holds the outcome. A change to its shape takes a new key, so a node that does not
    /// know it re-claims the row as before rather than misreading it.
    /// </summary>
    public const string DataKey = "outcome.v1";

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// The handler's failure message, cut as the row's column cuts it, so the trigger never carries more than the row
    /// could keep.
    /// </summary>
    public string? ErrorMessage { get; init; } = ErrorMessage is { Length: > BackgroundJobStateRepository.ErrorMessageMaxLength }
        ? ErrorMessage[..BackgroundJobStateRepository.ErrorMessageMaxLength]
        : ErrorMessage;

    /// <summary>Whether an attempt whose write is <paramref name="write"/> has an outcome to carry.</summary>
    public static bool Carries(AttemptRowWrite write) => write is AttemptRowWrite.Completed or AttemptRowWrite.Failed;

    /// <summary>
    /// The outcome <paramref name="data"/> carries, or <see langword="null"/> when it carries none, as a trigger from an
    /// earlier version does, or one this version cannot read.
    /// </summary>
    public static UnsavedOutcome? TryRead(JobDataMap data)
    {
        if (data.GetString(DataKey) is not { } json)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<UnsavedOutcome>(json, _options) is { } outcome && outcome.IsReadable
                ? outcome
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The outcome as trigger data stores it.</summary>
    public string Serialize() => JsonSerializer.Serialize(this, _options);

    // Only an outcome the row can take is read: a write that throws for its input would be re-fired for good. A failure
    // is written with its message, and the row's times are UTC.
    private bool IsReadable =>
        Attempt > 0
        && Carries(Write)
        && (Write is AttemptRowWrite.Completed || !string.IsNullOrWhiteSpace(ErrorMessage))
        && ClaimedAt.Kind is DateTimeKind.Utc
        && EndedAt.Kind is DateTimeKind.Utc;
}
