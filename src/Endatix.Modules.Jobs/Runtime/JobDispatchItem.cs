namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Carries the job type so a dispatch strategy can route an item without reading its job row.
/// </summary>
public readonly record struct JobDispatchItem(long JobId, string JobType);
