namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// One piece of work offered to the runner: which job, and of what type.
/// </summary>
internal readonly record struct JobDispatchItem(long JobId, string JobType);
