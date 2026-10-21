using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// An inline outbox handler whose work can also run as a background job. Register it with
/// <see cref="OutboxWorkServiceCollectionExtensions.AddOutboxWork{TWork}"/>, which registers the inline handler,
/// its job and the job's subscriptions together, so the relay can deliver either way.
/// </summary>
/// <remarks>
/// The relay runs <see cref="IOutboxIntegrationEventHandler.HandleAsync"/>; the job runs <see cref="Read"/> on the
/// same message and then the work it returns. This interface supplies both
/// <see cref="IOutboxIntegrationEventHandler.EventTypes"/> and
/// <see cref="IOutboxIntegrationEventHandler.HandleAsync"/>, so a work declares
/// <see cref="HandledEventTypes"/>, <see cref="JobType"/> and <see cref="Read"/> only.
/// </remarks>
/// <typeparam name="TSelf">The work type.</typeparam>
public interface IOutboxWork<TSelf> : IOutboxIntegrationEventHandler
    where TSelf : class, IOutboxWork<TSelf>
{
    /// <summary>
    /// The job type literal persisted on queued jobs. Declare it once and never rename it: a rename leaves jobs
    /// already queued with no handler.
    /// </summary>
    static abstract string JobType { get; }

    /// <summary>
    /// The event types the handler, and its job, run on, readable before any instance exists so the subscriptions
    /// can be registered from it.
    /// </summary>
    static abstract IReadOnlyCollection<string> HandledEventTypes { get; }

    /// <summary>
    /// Reads the work from <paramref name="message"/>, or <see cref="OutboxWork.None"/> when the message needs
    /// nothing done.
    /// </summary>
    /// <remarks>
    /// Only parse the message here; do every read and write in the returned work. <see cref="InvalidOperationException"/>
    /// and <see cref="System.Text.Json.JsonException"/> fail the job for good, so a transient exception thrown here
    /// would never be retried. <c>onlyWhen</c> on <see cref="OutboxWorkServiceCollectionExtensions.AddOutboxWork{TWork}"/>
    /// is a separate static predicate: the relay does not call this to decide.
    /// </remarks>
    OutboxWork Read(IOutboxMessage message);

    /// <inheritdoc cref="IOutboxIntegrationEventHandler.EventTypes"/>
    IReadOnlyCollection<string> IOutboxIntegrationEventHandler.EventTypes => TSelf.HandledEventTypes;

    /// <inheritdoc cref="IOutboxIntegrationEventHandler.HandleAsync"/>
    Task IOutboxIntegrationEventHandler.HandleAsync(IOutboxMessage message, CancellationToken cancellationToken) =>
        Read(message).RunAsync(cancellationToken);
}

/// <summary>One outbox message's work, or <see cref="None"/> when the message needs nothing done.</summary>
public sealed class OutboxWork
{
    private readonly Func<CancellationToken, Task>? _run;

    private OutboxWork(Func<CancellationToken, Task>? run) => _run = run;

    /// <summary>Nothing to do.</summary>
    public static OutboxWork None { get; } = new(null);

    /// <summary>The work <paramref name="run"/> does.</summary>
    public static OutboxWork For(Func<CancellationToken, Task> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new OutboxWork(run);
    }

    /// <summary>The work <paramref name="run"/> does for <paramref name="input"/>.</summary>
    public static OutboxWork For<T>(T input, Func<T, CancellationToken, Task> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return For(cancellationToken => run(input, cancellationToken));
    }

    /// <summary>
    /// <see cref="None"/> when <paramref name="input"/> is null, otherwise the work <paramref name="run"/> does.
    /// </summary>
    public static OutboxWork If<T>(T? input, Func<T, CancellationToken, Task> run)
        where T : class =>
        input is null ? None : For(input, run);

    /// <summary>Whether the message needs nothing done.</summary>
    public bool IsNone => _run is null;

    /// <summary>Does the work. Throws on a transient problem, so the relay or the job retries.</summary>
    public Task RunAsync(CancellationToken cancellationToken) =>
        _run is null ? Task.CompletedTask : _run(cancellationToken);
}
