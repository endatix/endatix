namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>Scheduled maintenance work. Each firing runs in a new scope on a Jobs worker.</summary>
public interface IRecurringJob
{
    /// <summary>Runs once; cancellation requests that the worker stop.</summary>
    Task RunAsync(CancellationToken cancellationToken);
}
