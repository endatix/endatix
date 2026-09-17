namespace Endatix.Modules.Jobs.Runtime;

internal sealed class NoOpJobMetrics : IJobMetrics
{
    public void Record(JobLifecycleEvent lifecycleEvent, string jobType)
    {
    }

    public void ObserveQueueDepth(int depth)
    {
    }

    public void ObserveBacklog(string jobType, int count, TimeSpan oldestWait)
    {
    }
}
