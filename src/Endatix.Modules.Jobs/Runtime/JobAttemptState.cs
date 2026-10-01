using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>What a running attempt needs to know about its row: whether it was cancelled or taken over.</summary>
internal sealed record JobAttemptState(JobStatus Status, int AttemptCount);
