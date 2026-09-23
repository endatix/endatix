using System.Diagnostics;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Multitenancy;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute.Core;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class BackgroundJobExecutorTests : IDisposable
{
    private const long JobId = 42;
    private const string EchoJobType = "Test.Echo";
    private const string PayloadJson = """{"a":1}""";
    private const long JobTenantId = 7;

    private static readonly DateTime _utcNow = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly IBackgroundJobStateRepository _stateRepository = Substitute.For<IBackgroundJobStateRepository>();
    private readonly RecordingJobMetrics _metrics = new();
    private readonly RecordingLogger _logger = new();
    private readonly BackgroundJobsOptions _options = new();
    private readonly List<ServiceProvider> _hosts = [];
    private readonly ActivityListener _activityListener;

    public BackgroundJobExecutorTests()
    {
        // A write lands unless a test says otherwise; without this every outcome would read as lost.
        _stateRepository
            .TryCompleteAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _stateRepository
            .TryFailAsync(
                Arg.Any<long>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _stateRepository
            .RecordFailedAttemptAsync(
                Arg.Any<long>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<DateTime>(),
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(true);

        // Without a listener the source creates no activity at all, so a handler could not see the one it runs in.
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Endatix.Jobs",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    /// <summary>The failure a handler returns, with the message the job row has to end up with.</summary>
    public static TheoryData<FailureShape, string> FailureMessages => new()
    {
        { FailureShape.ErrorMessage, "Form 42 has no schema." },
        { FailureShape.ValidationErrorMessage, "Payload is missing formId." },
        { FailureShape.NoMessage, "The job failed." },
        { FailureShape.NoValidationErrors, "The job failed." },
        { FailureShape.NullValidationError, "The job failed." },
    };

    /// <summary>
    /// The trace stored on the row, with the trace and parent the execution has to run under. A row whose trace is
    /// missing or unusable is traced on its own, so only the existence of a trace id is required of those.
    /// </summary>
    public static TheoryData<string?, string, string> StoredTraces => new()
    {
        {
            "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
            "0af7651916cd43dd8448eb211c80319c",
            "b7ad6b7169203331"
        },
        { null, "*", "0000000000000000" },
        { "not-a-trace", "*", "0000000000000000" },
    };

    public enum FailureShape
    {
        ErrorMessage,
        ValidationErrorMessage,
        NoMessage,

        /// <summary>A handler's own Result, whose validation errors are whatever it passed - here nothing at all.</summary>
        NoValidationErrors,

        /// <summary>A handler's own Result, whose validation errors hold an entry that is null.</summary>
        NullValidationError,
    }

    [Fact]
    public async Task RunAsync_ClaimLost_InvokesNothing()
    {
        // Arrange
        var handler = HandlerReturning(Result.Success());
        var executor = CreateExecutor(handler);
        ClaimReturns(null);

        // Act
        await RunAsync(executor);

        // Assert
        handler.Invocations.Should().BeEmpty();
        CalledRepositoryMethods().Should().ContainSingle()
            .Which.Should().Be(nameof(IBackgroundJobStateRepository.TryClaimAsync));
        _metrics.Records.Should().BeEmpty();
        _metrics.Durations.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_ClaimThrows_InvokesNothing()
    {
        // Arrange
        var handler = HandlerReturning(Result.Success());
        var executor = CreateExecutor(handler);
        _stateRepository
            .TryClaimAsync(JobId, Arg.Any<IReadOnlyCollection<string>>(), _utcNow, Arg.Any<CancellationToken>())
            .Returns<ClaimedJob?>(_ => throw new TimeoutException("The claim timed out."));

        // Act
        await RunAsync(executor);

        // Assert
        handler.Invocations.Should().BeEmpty();
        CalledRepositoryMethods().Should().ContainSingle()
            .Which.Should().Be(nameof(IBackgroundJobStateRepository.TryClaimAsync));
        _metrics.Records.Should().BeEmpty();
        _logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error)
            .Which.Exception.Should().BeOfType<TimeoutException>();
    }

    [Fact]
    public async Task RunAsync_Claimed_PassesRowContextWithoutAmbientTenant()
    {
        // Arrange
        var handler = HandlerReturning(Result.Success());
        var executor = CreateExecutor(handler);
        ClaimReturns(Claimed());

        // Act
        await RunAsync(executor);

        // Assert
        var invocation = handler.Invocations.Should().ContainSingle().Subject;
        invocation.Context.Should().Be(new BackgroundJobContext(JobId, EchoJobType, JobTenantId, PayloadJson, 1));
        invocation.AmbientTenantId.Should().Be(0, "a handler scopes its own queries to the job's tenant");
    }

    [Fact]
    public async Task RunAsync_Success_CompletesJob()
    {
        // Arrange
        var executor = CreateExecutor(HandlerReturning(Result.Success()));
        ClaimReturns(Claimed());

        // Act
        await RunAsync(executor);

        // Assert
        await _stateRepository.Received(1).TryClaimAsync(
            JobId,
            Arg.Is<IReadOnlyCollection<string>>(jobTypes => jobTypes.Contains(EchoJobType)),
            _utcNow,
            Arg.Any<CancellationToken>());
        await _stateRepository.Received(1).TryCompleteAsync(JobId, 1, _utcNow, Arg.Any<CancellationToken>());
        _metrics.Records.Should().Equal(
            (JobLifecycleEvent.Claimed, EchoJobType),
            (JobLifecycleEvent.Completed, EchoJobType));
        _metrics.Durations.Should().ContainSingle()
            .Which.Should().Be((EchoJobType, TimeSpan.Zero, JobAttemptOutcome.Completed));
    }

    [Theory]
    [MemberData(nameof(FailureMessages))]
    public async Task RunAsync_FailureResult_FailsWithResultMessage(FailureShape shape, string expectedMessage)
    {
        // Arrange
        var executor = CreateExecutor(HandlerReturning(Failure(shape)));
        ClaimReturns(Claimed());

        // Act
        await RunAsync(executor);

        // Assert
        await _stateRepository.Received(1).TryFailAsync(
            JobId, 1, expectedMessage, _utcNow, Arg.Any<CancellationToken>());
        CalledRepositoryMethods().Should().NotContain(nameof(IBackgroundJobStateRepository.RecordFailedAttemptAsync));
        _metrics.Records.Should().Contain((JobLifecycleEvent.Failed, EchoJobType));
        _metrics.Durations.Should().ContainSingle().Which.Outcome.Should().Be(JobAttemptOutcome.Failed);
    }

    [Fact]
    public async Task RunAsync_ThrowsWithSecretMessage_RecordsFallbackOnly()
    {
        // Arrange
        var connectionString = "Host=db.internal;Username=endatix;Password=s3cret";
        var executor = CreateExecutor(HandlerThrowing(new InvalidOperationException(connectionString)));
        ClaimReturns(Claimed());

        // Act
        await RunAsync(executor);

        // Assert
        await _stateRepository.Received(1).RecordFailedAttemptAsync(
            JobId,
            1,
            3,
            _utcNow.AddSeconds(30),
            "The job could not be completed.",
            _utcNow,
            Arg.Any<CancellationToken>());
        FailedAttemptMessage().Should().NotContain("Password");
        var errorLog = _logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error).Subject;
        errorLog.Exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(connectionString);
        errorLog.Message.Should().Contain(JobId.ToString());
    }

    [Fact]
    public async Task RunAsync_ThrowsEndUserSafeError_RecordsAuthoredMessage()
    {
        // Arrange
        var executor = CreateExecutor(HandlerThrowing(new DomainRuleException("Export format 9 is disabled.")));
        ClaimReturns(Claimed());

        // Act
        await RunAsync(executor);

        // Assert
        FailedAttemptMessage().Should().Be("Export format 9 is disabled.");
        _logger.Entries.Should().NotContain(entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RunAsync_PerTypeMaxAttempts_ChoosesRetryOrDeadLetter()
    {
        // Arrange
        const long shortLivedJobId = 42;
        const long longLivedJobId = 43;
        _options.JobTypes["ShortLived"] = new BackgroundJobTypeOptions { MaxAttempts = 8 };
        var shortLived = CreateExecutor(HandlerThrowing(new TimeoutException("The export timed out."), "ShortLived"));
        var longLived = CreateExecutor(HandlerThrowing(new TimeoutException("The export timed out."), "LongLived"));
        ClaimReturns(Claimed(shortLivedJobId, "ShortLived", attemptCount: 3), shortLivedJobId);
        ClaimReturns(Claimed(longLivedJobId, "LongLived", attemptCount: 3), longLivedJobId);

        // Act
        await RunAsync(shortLived, shortLivedJobId, "ShortLived");
        await RunAsync(longLived, longLivedJobId, "LongLived");

        // Assert
        await _stateRepository.Received(1).RecordFailedAttemptAsync(
            shortLivedJobId,
            3,
            8,
            // The third attempt waits twice as long as the second, from the same base as the global policy.
            _utcNow.AddSeconds(120),
            Arg.Any<string>(),
            _utcNow,
            Arg.Any<CancellationToken>());
        await _stateRepository.Received(1).RecordFailedAttemptAsync(
            longLivedJobId,
            3,
            3,
            Arg.Any<DateTime>(),
            Arg.Any<string>(),
            _utcNow,
            Arg.Any<CancellationToken>());
        _metrics.Records.Should()
            .Contain((JobLifecycleEvent.RetryScheduled, "ShortLived"))
            .And.Contain((JobLifecycleEvent.DeadLettered, "LongLived"));
        _metrics.Durations.Select(duration => (duration.JobType, duration.Outcome)).Should().Equal(
            ("ShortLived", JobAttemptOutcome.RetryScheduled),
            ("LongLived", JobAttemptOutcome.DeadLettered));
    }

    [Fact]
    public async Task RunAsync_OutcomeWriteLostOrThrows_LogsAndReturns()
    {
        // Arrange
        var executor = CreateExecutor(HandlerReturning(Result.Success()));
        ClaimReturns(Claimed());
        _stateRepository.TryCompleteAsync(JobId, 1, _utcNow, Arg.Any<CancellationToken>())
            .Returns(_ => false, _ => throw new TimeoutException("The write timed out."));

        // Act
        await RunAsync(executor);
        await RunAsync(executor);

        // Assert
        CalledRepositoryMethods().Should().OnlyContain(method =>
            method == nameof(IBackgroundJobStateRepository.TryClaimAsync) ||
            method == nameof(IBackgroundJobStateRepository.TryCompleteAsync));
        _logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning);
        _logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error)
            .Which.Exception.Should().BeOfType<TimeoutException>();
        _metrics.Records.Should().NotContain((JobLifecycleEvent.Completed, EchoJobType));
        _metrics.Durations.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_MetricsThrow_RecordsOutcomeAnyway()
    {
        // Arrange
        _metrics.Throws = true;
        var executor = CreateExecutor(HandlerReturning(Result.Success()));
        ClaimReturns(Claimed());

        // Act
        await RunAsync(executor);

        // Assert
        await _stateRepository.Received(1).TryCompleteAsync(JobId, 1, _utcNow, Arg.Any<CancellationToken>());
        _logger.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task RunAsync_Success_LeavesExecutionStatusUnset()
    {
        // Arrange
        var handler = HandlerReturning(Result.Success());
        var executor = CreateExecutor(handler);
        ClaimReturns(Claimed());

        // Act
        await RunAsync(executor);

        // Assert
        var activity = ExecutionActivity(handler);
        activity.Status.Should().Be(ActivityStatusCode.Unset);
        activity.StatusDescription.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_FailureResult_MarksExecutionErrorWithoutDescription()
    {
        // Arrange
        var handler = HandlerReturning(Result.Error("Form 42 has no schema."));
        var executor = CreateExecutor(handler);
        ClaimReturns(Claimed());

        // Act
        await RunAsync(executor);

        // Assert
        var activity = ExecutionActivity(handler);
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().BeNull("the handler's own message is already on the job row");
    }

    [Fact]
    public async Task RunAsync_ThrowsWithSecretMessage_MarksExecutionErrorWithTypeNameOnly()
    {
        // Arrange
        var connectionString = "Host=db.internal;Username=endatix;Password=s3cret";
        var handler = HandlerThrowing(new InvalidOperationException(connectionString));
        var executor = CreateExecutor(handler);
        ClaimReturns(Claimed());

        // Act
        await RunAsync(executor);

        // Assert
        var activity = ExecutionActivity(handler);
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be(nameof(InvalidOperationException));
        connectionString.Split(';', '=').Should().AllSatisfy(part =>
            activity.StatusDescription.Should().NotContain(part));
    }

    [Theory]
    [MemberData(nameof(StoredTraces))]
    public async Task RunAsync_TraceId_ReparentsExecutionActivity(
        string? storedTraceId,
        string expectedTraceId,
        string expectedParentSpanId)
    {
        // Arrange
        var handler = HandlerReturning(Result.Success());
        var executor = CreateExecutor(handler);
        ClaimReturns(Claimed(traceId: storedTraceId));

        // Act
        await RunAsync(executor);

        // Assert
        var activity = handler.Invocations.Should().ContainSingle().Subject.Activity;
        activity.Should().NotBeNull();
        activity!.Source.Name.Should().Be("Endatix.Jobs");
        activity.Kind.Should().Be(ActivityKind.Consumer);
        activity.TraceId.ToHexString().Should().Match(expectedTraceId);
        activity.ParentSpanId.ToHexString().Should().Be(expectedParentSpanId);
        activity.GetTagItem("endatix.job.id").Should().Be(JobId);
        activity.GetTagItem("endatix.job.type").Should().Be(EchoJobType);
        activity.GetTagItem("endatix.job.attempt").Should().Be(1);
        _logger.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Warning);
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        foreach (var host in _hosts)
        {
            host.Dispose();
        }
    }

    private BackgroundJobExecutor CreateExecutor(RecordingJobHandler handler)
    {
        var host = new ServiceCollection()
            .AddSingleton(_ => _stateRepository)
            .AddScoped<ITenantContext, TenantContext>()
            .AddScoped<IBackgroundJobHandler>(scope =>
                new ScopedJobHandler(handler, scope.GetRequiredService<ITenantContext>()))
            .BuildServiceProvider();
        _hosts.Add(host);

        var scopeFactory = host.GetRequiredService<IServiceScopeFactory>();

        return new BackgroundJobExecutor(
            scopeFactory,
            new BackgroundJobHandlerRegistry(scopeFactory),
            new JobRowExecutionContextResolver(),
            new FixedClock(_utcNow),
            Options.Create(_options),
            _metrics,
            _logger);
    }

    private static Task RunAsync(BackgroundJobExecutor executor, long jobId = JobId, string jobType = EchoJobType) =>
        executor.RunAsync(new JobDispatchItem(jobId, jobType), TestContext.Current.CancellationToken);

    private static ClaimedJob Claimed(
        long jobId = JobId,
        string jobType = EchoJobType,
        int attemptCount = 1,
        string? traceId = null) =>
        new(jobId, jobType, JobTenantId, PayloadJson, attemptCount, traceId, JobStatus.Processing);

    private void ClaimReturns(ClaimedJob? claimed, long jobId = JobId) =>
        _stateRepository.TryClaimAsync(
                jobId, Arg.Any<IReadOnlyCollection<string>>(), _utcNow, Arg.Any<CancellationToken>())
            .Returns(claimed);

    private static RecordingJobHandler HandlerReturning(Result result, string jobType = EchoJobType) =>
        new(jobType, () => result);

    private static RecordingJobHandler HandlerThrowing(Exception exception, string jobType = EchoJobType) =>
        new(jobType, () => throw exception);

    private static Result Failure(FailureShape shape) => shape switch
    {
        FailureShape.ErrorMessage => Result.Error("Form 42 has no schema."),
        FailureShape.ValidationErrorMessage =>
            Result.Invalid(new ValidationError { ErrorMessage = "Payload is missing formId." }),
        FailureShape.NoValidationErrors => Result.Invalid((IEnumerable<ValidationError>)null!),
        FailureShape.NullValidationError => Result.Invalid(new ValidationError[] { null! }),
        _ => Result.Error(),
    };

    private List<string> CalledRepositoryMethods() =>
        [.. _stateRepository.ReceivedCalls().Select(call => call.GetMethodInfo().Name)];

    /// <summary>The activity the one handler invocation ran in, as the attempt left it.</summary>
    private static Activity ExecutionActivity(RecordingJobHandler handler) =>
        handler.Invocations.Should().ContainSingle().Subject.Activity
        ?? throw new InvalidOperationException("The handler ran outside an activity.");

    /// <summary>The message argument of the one failed-attempt write the executor made.</summary>
    private string FailedAttemptMessage() =>
        (string)_stateRepository.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IBackgroundJobStateRepository.RecordFailedAttemptAsync))
            .GetArguments()[4]!;

    /// <summary>
    /// The handler a scenario scripts. It is resolved from the execution scope, so the tenant it records is the one
    /// that scope resolves rather than one the test set.
    /// </summary>
    private sealed class ScopedJobHandler(RecordingJobHandler handler, ITenantContext tenantContext)
        : IBackgroundJobHandler
    {
        public string JobType => handler.JobType;

        public Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken) =>
            handler.ExecuteAsync(job, tenantContext.TenantId);
    }

    private sealed class RecordingJobHandler(string jobType, Func<Result> respond)
    {
        public string JobType => jobType;

        public List<HandlerInvocation> Invocations { get; } = [];

        public Task<Result> ExecuteAsync(BackgroundJobContext job, long ambientTenantId)
        {
            Invocations.Add(new HandlerInvocation(job, ambientTenantId, Activity.Current));

            return Task.FromResult(respond());
        }
    }

    private sealed record HandlerInvocation(BackgroundJobContext Context, long AmbientTenantId, Activity? Activity);

    private sealed class RecordingJobMetrics : IJobMetrics
    {
        /// <summary>Makes the sink fail every call, the way a host-supplied one can.</summary>
        public bool Throws { get; set; }

        public List<(JobLifecycleEvent LifecycleEvent, string JobType)> Records { get; } = [];

        public List<(string JobType, TimeSpan Duration, JobAttemptOutcome Outcome)> Durations { get; } = [];

        public void Record(JobLifecycleEvent lifecycleEvent, string jobType)
        {
            if (Throws)
            {
                throw Unavailable();
            }

            Records.Add((lifecycleEvent, jobType));
        }

        public void ObserveQueueDepth(int depth) => throw SweeperOnly();

        public void ObserveBacklog(string jobType, int count, TimeSpan oldestWait) => throw SweeperOnly();

        public void ObserveDuration(string jobType, TimeSpan duration, JobAttemptOutcome outcome)
        {
            if (Throws)
            {
                throw Unavailable();
            }

            Durations.Add((jobType, duration, outcome));
        }

        private static InvalidOperationException Unavailable() => new("The metrics sink is unavailable.");

        private static NotSupportedException SweeperOnly() =>
            new("Running a job observes its duration only; the sweeper observes the queue and the backlog.");
    }

    private sealed class RecordingLogger : ILogger<BackgroundJobExecutor>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTimeOffset Now { get; } = new(utcNow);
    }
}
