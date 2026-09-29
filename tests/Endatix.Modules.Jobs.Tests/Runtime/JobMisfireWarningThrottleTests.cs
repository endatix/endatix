using System.Diagnostics.Metrics;
using Endatix.Core.Abstractions;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class JobMisfireWarningThrottleTests : IDisposable
{
    private const string WebHook = "WebHookDelivery";
    private const string Export = "SubmissionExport";

    private static readonly DateTimeOffset Start = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();
    private readonly MeterListener _meterListener = new();
    private readonly List<string?> _counted = [];
    private readonly RecordingLogger _logger = new();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    public JobMisfireWarningThrottleTests()
    {
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "endatix.jobs.misfired")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            _counted.Add(tags.ToArray().Single(tag => tag.Key == "job_type").Value as string));
        _meterListener.Start();
        _clock.UtcNow.Returns(Start);
    }

    [Fact]
    public async Task TriggerMisfired_BacklogWithinOneThreshold_WarnsOnceAndCountsEvery()
    {
        // Arrange
        var listener = CreateListener(thresholdSeconds: 60);

        // Act
        for (var jobId = 1; jobId <= 50; jobId++)
        {
            await listener.TriggerMisfired(TriggerOf(WebHook, jobId), Substitute.For<IScheduler>(), TestContext.Current.CancellationToken);
        }

        // Assert
        _counted.Should().HaveCount(50).And.OnlyContain(jobType => jobType == WebHook);
        _logger.Messages.Should().ContainSingle()
            .Which.Should().Contain("waited past the misfire threshold").And.Contain(WebHook).And.Contain("1 triggers");
    }

    [Fact]
    public async Task TriggerMisfired_AfterThresholdPasses_WarnsAgainWithTheCountSinceLastWarning()
    {
        // Arrange
        var listener = CreateListener(thresholdSeconds: 60);
        await listener.TriggerMisfired(TriggerOf(WebHook, 1), Substitute.For<IScheduler>(), TestContext.Current.CancellationToken);
        for (var jobId = 2; jobId <= 4; jobId++)
        {
            await listener.TriggerMisfired(TriggerOf(WebHook, jobId), Substitute.For<IScheduler>(), TestContext.Current.CancellationToken);
        }

        _clock.UtcNow.Returns(Start.AddSeconds(60));

        // Act
        await listener.TriggerMisfired(TriggerOf(WebHook, 5), Substitute.For<IScheduler>(), TestContext.Current.CancellationToken);

        // Assert — the second warning stands for the three throttled misfires and its own.
        _logger.Messages.Should().HaveCount(2);
        _logger.Messages[1].Should().Contain("Background job 5").And.Contain("4 triggers");
        _counted.Should().HaveCount(5);
    }

    [Fact]
    public async Task TriggerMisfired_OtherJobTypeWithinThreshold_WarnsForEachType()
    {
        // Arrange
        var listener = CreateListener(thresholdSeconds: 60);
        await listener.TriggerMisfired(TriggerOf(WebHook, 1), Substitute.For<IScheduler>(), TestContext.Current.CancellationToken);

        // Act
        await listener.TriggerMisfired(TriggerOf(Export, 2), Substitute.For<IScheduler>(), TestContext.Current.CancellationToken);

        // Assert
        _logger.Messages.Should().HaveCount(2);
        _logger.Messages[1].Should().Contain(Export);
    }

    public void Dispose()
    {
        _meterListener.Dispose();
        _services.Dispose();
    }

    private JobMisfireListener CreateListener(int thresholdSeconds) =>
        new(
            _services.GetRequiredService<IMeterFactory>(),
            _clock,
            Options.Create(new BackgroundJobsOptions { MisfireThresholdSeconds = thresholdSeconds }),
            _logger);

    private static ITrigger TriggerOf(string jobType, long jobId) =>
        TriggerBuilder.Create()
            .WithIdentity(jobId.ToString(), jobType)
            .ForJob(QuartzRegistration.JobKeyFor(jobType))
            .StartNow()
            .Build();

    private sealed class RecordingLogger : ILogger<JobMisfireListener>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
