using Quartz.Impl.AdoJobStore;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The execution groups with at least one free slot on this node, and how many triggers each may still take, as
/// one trigger acquisition sees them.
/// </summary>
/// <param name="Groups">The execution groups with at least one free slot on this node.</param>
/// <param name="Slots">How many triggers the group at the same index in <paramref name="Groups"/> may still take.</param>
internal sealed record ExecutionGroupFreeSlots(string[] Groups, int[] Slots)
{
    /// <summary>
    /// The free slots in the limits the scheduler thread passes with an acquisition, which it has already lowered by
    /// what this node is running. A group without a limit may fill the whole batch.
    /// </summary>
    public static ExecutionGroupFreeSlots Of(TriggerAcquisitionCriteria criteria)
    {
        var free = criteria.ExecutionLimits?.Groups
            .Where(limit => limit.Group.Name is not null && limit.MaxConcurrent is null or > 0)
            .ToArray() ?? [];

        return new ExecutionGroupFreeSlots(
            free.Select(limit => limit.Group.Name!).ToArray(),
            free.Select(limit => limit.MaxConcurrent ?? criteria.MaxCount).ToArray());
    }

    /// <summary>Whether no group has a free slot, so the acquisition can take nothing.</summary>
    public bool IsEmpty => Groups.Length == 0;
}
