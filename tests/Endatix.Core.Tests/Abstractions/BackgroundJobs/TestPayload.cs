using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Core.Tests.Abstractions.BackgroundJobs;

internal sealed record TestPayload(long Id) : IBackgroundJobPayload
{
    public static string JobType => "Test";
}
