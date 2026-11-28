namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>Scheduled maintenance work. Each firing runs in a new scope on a Jobs worker.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0008")]
public interface IRecurringJob
{
    /// <summary>Runs once; cancellation requests that the worker stop.</summary>
    Task RunAsync(CancellationToken cancellationToken);
}
