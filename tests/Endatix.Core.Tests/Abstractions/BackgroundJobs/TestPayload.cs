using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Core.Tests.Abstractions.BackgroundJobs;

internal sealed record TestPayload(long Id) : IBackgroundJobPayload
{
    public static string JobType => "Test";
}

/// <summary>
/// A payload type the reader cannot bind: its constructor parameter matches no property. Serializing it succeeds,
/// so only reading reveals the mistake.
/// </summary>
internal sealed class UnbindablePayload(long code) : IBackgroundJobPayload
{
    public static string JobType => "Unbindable";

    public long Id { get; } = code;
}
