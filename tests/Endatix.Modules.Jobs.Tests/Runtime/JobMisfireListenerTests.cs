using System.Diagnostics.Metrics;
using Endatix.Core.Abstractions;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
            // Only this test's meters: other tests create the same instrument at the same time.
            if (instrument.Name == "endatix.jobs.misfired"
                && ReferenceEquals(instrument.Meter.Scope, _services.GetRequiredService<IMeterFactory>()))
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
        var listener = CreateListener();
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
        var listener = CreateListener();
        var trigger = TriggerBuilder.Create().WithIdentity(JobRetentionJob.Group, QuartzRegistration.MaintenanceJobGroup)
            .ForJob(JobRetentionJob.Group, QuartzRegistration.MaintenanceJobGroup).StartNow().Build();

        // Act
        await listener.TriggerMisfired(trigger, Substitute.For<IScheduler>(), TestContext.Current.CancellationToken);

        // Assert
        _misfiredJobTypes.Should().BeEmpty();
    }

    private JobMisfireListener CreateListener() =>
        new(
            _services.GetRequiredService<IMeterFactory>(),
            Substitute.For<IDateTimeProvider>(),
            Options.Create(new BackgroundJobsOptions()),
            NullLogger<JobMisfireListener>.Instance);

    public void Dispose()
    {
        _listener.Dispose();
        _services.Dispose();
    }
}
