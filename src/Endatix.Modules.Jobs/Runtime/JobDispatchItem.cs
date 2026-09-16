namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// One piece of work offered to the runner: which job, and of what type.
/// </summary>
/// <remarks>
/// The job type travels with the id so that whatever routes the item — a single pool today, a
/// per-workload-class strategy later — can decide where it goes without reading the row back.
/// </remarks>
/// <param name="JobId">Identifier of the job row.</param>
/// <param name="JobType">The job's router key, compared ordinal and case-sensitive.</param>
public readonly record struct JobDispatchItem(long JobId, string JobType);
