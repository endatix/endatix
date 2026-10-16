using Endatix.Core.Abstractions;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>A read-only database is retried without end, so why is said, once a minute rather than per job.</summary>
public sealed class ReadOnlyDatabaseWarningTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly ILogger<ReadOnlyDatabaseWarning> _logger = Substitute.For<ILogger<ReadOnlyDatabaseWarning>>();

    [Fact]
    public void WarnIfReadOnly_ReadOnlyFailuresWithinAMinute_WarnsOnce()
    {
        // Arrange
        var warning = new ReadOnlyDatabaseWarning(_clock, _logger);
        _clock.UtcNow.Returns(Start, Start.AddSeconds(30));

        // Act
        warning.WarnIfReadOnly(ReadOnly());
        warning.WarnIfReadOnly(ReadOnly());

        // Assert
        Warnings().Should().Be(1);
    }

    [Fact]
    public void WarnIfReadOnly_ReadOnlyFailureAMinuteLater_WarnsAgain()
    {
        // Arrange
        var warning = new ReadOnlyDatabaseWarning(_clock, _logger);
        _clock.UtcNow.Returns(Start, Start + ReadOnlyDatabaseWarning.Interval);

        // Act
        warning.WarnIfReadOnly(ReadOnly());
        warning.WarnIfReadOnly(ReadOnly());

        // Assert
        Warnings().Should().Be(2);
    }

    [Fact]
    public void WarnIfReadOnly_OtherOutage_SaysNothing()
    {
        // Arrange
        var warning = new ReadOnlyDatabaseWarning(_clock, _logger);
        _clock.UtcNow.Returns(Start);

        // Act
        warning.WarnIfReadOnly(new InvalidOperationException(
            "The write failed.",
            new PostgresException("terminating connection due to administrator command", "FATAL", "FATAL", PostgresErrorCodes.AdminShutdown)));

        // Assert
        Warnings().Should().Be(0);
    }

    private static InvalidOperationException ReadOnly() => new(
        "The write failed.",
        new PostgresException("cannot execute UPDATE in a read-only transaction", "ERROR", "ERROR", PostgresErrorCodes.ReadOnlySqlTransaction));

    private int Warnings() =>
        _logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ILogger.Log) && (LogLevel)call.GetArguments()[0]! == LogLevel.Warning);
}
