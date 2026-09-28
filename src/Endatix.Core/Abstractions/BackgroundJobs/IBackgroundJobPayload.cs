namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>
/// The typed input of one job type. A payload names the job type it belongs to, next to the data it
/// carries, so a caller cannot pair a payload with the wrong handler.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="JobType"/> is persisted on every job row and in the scheduler's job keys. Declare it once, as
/// a string literal, and never derive it from a CLR type name: renaming or moving a class must not orphan
/// jobs that are still waiting to run. Renaming the literal is creating a new job type.
/// </para>
/// <para>
/// A payload written by one release must deserialize in the next, because a job may wait out a rolling
/// upgrade or retry for hours. Adding an optional property is safe; renaming, removing or retyping one is a
/// new job type. A constructor parameter is optional only when it has a default value, e.g.
/// <c>record X(long Id, bool Notify = false)</c>, even when its type is nullable:
/// <see cref="BackgroundJobPayloadSerializer"/> rejects stored input that lacks a parameter without a
/// default, so adding one makes every job already queued unreadable. Keep payloads thin — ids plus the
/// minimum non-personal data — and load current data by id when the job runs.
/// </para>
/// </remarks>
public interface IBackgroundJobPayload
{
    /// <summary>The job type this payload is the input of, e.g. <c>WebHookDelivery</c>.</summary>
    static abstract string JobType { get; }
}
