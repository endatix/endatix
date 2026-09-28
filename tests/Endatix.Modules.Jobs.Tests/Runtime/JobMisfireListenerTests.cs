using System.Diagnostics.Metrics;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class JobMisfireListenerTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();
    private readonly MeterListener _listener = new();
    private readonly List<string?> _misfiredJobTypes = [];

    public JobMisfireListenerTests()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "endatix.jobs.misfired")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            _misfiredJobTypes.Add(tags.ToArray().Single(tag => tag.Key == "job_type").Value as string));
        _listener.Start();
    }

    [Fact]
    public async Task TriggerMisfired_JobTypeTrigger_CountsItsJobType()
    {
        // Arrange
        var listener = new JobMisfireListener(
            _services.GetRequiredService<IMeterFactory>(), NullLogger<JobMisfireListener>.Instance);
        var trigger = TriggerBuilder.Create().WithIdentity("42", "WebHookDelivery")
            .ForJob(QuartzRegistration.JobKeyFor("WebHookDelivery")).StartNow().Build();

        // Act
        await listener.TriggerMisfired(trigger, Substitute.For<IScheduler>(), TestContext.Current.CancellationToken);

        // Assert
        _misfiredJobTypes.Should().Equal("WebHookDelivery");
    }

    [Fact]
    public async Task TriggerMisfired_MaintenanceTrigger_IsNotCounted()
    {
        // Arrange — retention runs late after an outage, which is not a job backlog.
        var listener = new JobMisfireListener(
            _services.GetRequiredService<IMeterFactory>(), NullLogger<JobMisfireListener>.Instance);
        var trigger = TriggerBuilder.Create().WithIdentity(JobRetentionJob.Group, QuartzRegistration.MaintenanceJobGroup)
            .ForJob(JobRetentionJob.Group, QuartzRegistration.MaintenanceJobGroup).StartNow().Build();

        // Act
        await listener.TriggerMisfired(trigger, Substitute.For<IScheduler>(), TestContext.Current.CancellationToken);

        // Assert
        _misfiredJobTypes.Should().BeEmpty();
    }

    public void Dispose()
    {
        _listener.Dispose();
        _services.Dispose();
    }
}
