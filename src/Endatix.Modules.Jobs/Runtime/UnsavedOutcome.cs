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
    /// The trigger data key that holds the outcome. A change to its shape that a reader of this key would misread
    /// takes a new key, so a node that does not know it re-claims the row as before; a field added with a default that
    /// changes nothing, as <see cref="Rejections"/> is, keeps the key, and a reader without it ignores it.
    /// </summary>
    public const string DataKey = "outcome.v1";

    /// <summary>
    /// How many firings carrying the outcome may find the database up but the write refused before the outcome is
    /// dropped and the row re-claimed. Such a refusal is the write's own fault — a constraint or a trigger on the
    /// table, say — and does not clear by trying again. Deadlocks, failovers and the other outages
    /// <see cref="DatabaseFailures"/> names are not counted at all; three firings, about half a minute with their write
    /// retries and re-fire delays, ride out an outage whose code that list misses while the row still reads, and hold
    /// a worker of the job type's cap for no longer than that.
    /// </summary>
    internal const int MaxRejections = 3;

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

    /// <summary>
    /// How many firings carrying the outcome found the database up but the write refused. An outage is not counted:
    /// the outcome is carried for as long as the database is away, as the row would wait for it.
    /// </summary>
    public int Rejections { get; init; }

    /// <summary>Whether the outcome has been refused <see cref="MaxRejections"/> times, and is no longer carried.</summary>
    [JsonIgnore]
    public bool IsSpent => Rejections >= MaxRejections;

    /// <summary>Whether an attempt whose write is <paramref name="write"/> has an outcome to carry.</summary>
    public static bool Carries(AttemptRowWrite write) => write is AttemptRowWrite.Completed or AttemptRowWrite.Failed;

    /// <summary>
    /// The outcome <paramref name="data"/> carries; or none, with why, when it carries one this version cannot read;
    /// or neither, when it carries none, as a trigger from an earlier version does.
    /// </summary>
    public static CarriedOutcome Read(JobDataMap data)
    {
        if (data.GetString(DataKey) is not { } json)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<UnsavedOutcome>(json, _options) is { } outcome
                ? outcome.Check()
                : new CarriedOutcome(null, "it is empty");
        }
        catch (JsonException exception)
        {
            // The path, never the message: the message can quote the value, which may hold the handler's text.
            return new CarriedOutcome(null, $"it does not parse as an outcome at {exception.Path ?? "$"}");
        }
    }

    /// <summary>The outcome, refused once more, as the re-fire that carries it again stores it.</summary>
    public UnsavedOutcome Rejected() => this with { Rejections = Rejections + 1 };

    /// <summary>The outcome as trigger data stores it.</summary>
    public string Serialize() => JsonSerializer.Serialize(this, _options);

    // Only an outcome the row can take is read: a write that throws for its input would be re-fired for good. A failure
    // is written with its message, and the row's times are UTC.
    private CarriedOutcome Check() => UnreadableReason() is { } reason ? new(null, reason) : new(this, null);

    private string? UnreadableReason() => this switch
    {
        { Attempt: <= 0 } => "its attempt is not positive",
        { Rejections: < 0 } => "its rejection count is negative",
        _ when !Carries(Write) => $"its write {Write} is not one an attempt's outcome makes",
        { Write: AttemptRowWrite.Failed } when string.IsNullOrWhiteSpace(ErrorMessage) => "its failure has no message",
        _ when ClaimedAt.Kind is not DateTimeKind.Utc || EndedAt.Kind is not DateTimeKind.Utc => "its times are not UTC",
        _ => null,
    };
}

/// <summary>
/// What a trigger's data carries as an outcome: the outcome, or, when it carries one this version cannot read, why;
/// neither when it carries none.
/// </summary>
internal readonly record struct CarriedOutcome(UnsavedOutcome? Outcome, string? Unreadable);
