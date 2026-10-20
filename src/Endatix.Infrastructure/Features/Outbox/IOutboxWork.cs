using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// An inline outbox handler whose work can also run as a background job. Register it with
/// <see cref="OutboxWorkServiceCollectionExtensions.AddOutboxWork{TWork, TPayload}"/>, which registers the inline
/// handler, its job and the job's subscriptions together, so the relay can deliver either way.
/// </summary>
/// <remarks>
/// The relay runs <see cref="IOutboxIntegrationEventHandler.HandleAsync"/>; the job runs <see cref="Read"/> on the
/// same message and then the work it returns, so both do the same thing.
/// </remarks>
public interface IOutboxWork : IOutboxIntegrationEventHandler
{
    /// <summary>
    /// The event types the handler, and its job, run on: what <see cref="IOutboxIntegrationEventHandler.EventTypes"/>
    /// returns, readable before any instance exists so the subscriptions can be registered from it.
    /// </summary>
    static abstract IReadOnlyCollection<string> HandledEventTypes { get; }

    /// <summary>
    /// Reads the work from <paramref name="message"/>, or <see cref="OutboxWork.None"/> when the message needs
    /// nothing done.
    /// </summary>
    /// <exception cref="InvalidOperationException">The message cannot be read, or names another tenant.</exception>
    /// <exception cref="System.Text.Json.JsonException">The payload is not JSON.</exception>
    OutboxWork Read(IOutboxMessage message);
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

    /// <summary>Whether the message needs nothing done.</summary>
    public bool IsNone => _run is null;

    /// <summary>Does the work. Throws on a transient problem, so the relay or the job retries.</summary>
    public Task RunAsync(CancellationToken cancellationToken) =>
        _run is null ? Task.CompletedTask : _run(cancellationToken);
}
