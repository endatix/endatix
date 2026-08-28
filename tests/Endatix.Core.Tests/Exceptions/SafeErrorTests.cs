using Endatix.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Tests.Exceptions;

/// <summary>
/// <see cref="SafeError"/> is the only gate between a caught exception and client-visible text, so its
/// two branches - opted in, and everything else - are what keep provider detail out of HTTP responses.
/// </summary>
public class SafeErrorTests
{
    private const string Fallback = "Could not add locale.";

    [Fact]
    public void MessageOr_WithDomainRuleException_ReturnsTheDomainMessage()
    {
        // Arrange
        DomainRuleException exception = new("A data list cannot have more than 25 cultures.");

        // Act
        var message = SafeError.MessageOr(exception, Fallback);

        // Assert
        message.Should().Be("A data list cannot have more than 25 cultures.");
    }

    [Fact]
    public void MessageOr_WithDomainValidationException_OmitsTheParameterName()
    {
        // Arrange
        DomainValidationException exception = new(
            "The synthetic 'default' key cannot be added as a culture.",
            "cultureCode");

        // Act
        var message = SafeError.MessageOr(exception, Fallback);

        // Assert
        message.Should().Be("The synthetic 'default' key cannot be added as a culture.");
        exception.Message.Should().Contain("cultureCode", "ArgumentException decorates Message, EndUserMessage must not");
    }

    [Theory]
    [InlineData("relation \"DataLists\" does not exist")]
    [InlineData("Host=db;Username=postgres;Password=hunter2")]
    public void MessageOr_WithProviderStyleException_ReturnsTheFallback(string providerText)
    {
        // Arrange
        InvalidOperationException exception = new(providerText);

        // Act
        var message = SafeError.MessageOr(exception, Fallback);

        // Assert
        message.Should().Be(Fallback);
        message.Should().NotContain(providerText);
    }

    [Fact]
    public void MessageOr_WithArgumentException_ReturnsTheFallback()
    {
        // Arrange - the same base type a domain-safe rejection uses; only the opt-in distinguishes them.
        ArgumentException exception = new("Value does not fall within the expected range.", "jsonObjectKey");

        // Act
        var message = SafeError.MessageOr(exception, Fallback);

        // Assert
        message.Should().Be(Fallback);
    }

    [Fact]
    public void MessageOr_WithNull_ReturnsTheFallback()
    {
        // Act
        var message = SafeError.MessageOr(null, Fallback);

        // Assert
        message.Should().Be(Fallback);
    }

    [Fact]
    public void MessageOr_WhenInnerExceptionCarriesProviderText_DoesNotSurfaceIt()
    {
        // Arrange
        InvalidOperationException inner = new("Npgsql: 28P01 password authentication failed");
        DomainRuleException exception = new("A data list cannot have more than 25 cultures.", inner);

        // Act
        var message = SafeError.MessageOr(exception, Fallback);

        // Assert
        message.Should().Be("A data list cannot have more than 25 cultures.");
        message.Should().NotContain("Npgsql");
    }

    /// <summary>
    /// <see cref="SafeError.LogAndResolve"/> exists so call sites stop re-deciding the severity: a rule
    /// the caller broke is informational, anything else is an error carrying the full exception.
    /// </summary>
    [Fact]
    public void LogAndResolve_WithSafeError_LogsInformationAndReturnsTheDomainMessage()
    {
        // Arrange
        RecordingLogger logger = new();
        DomainRuleException exception = new("A data list cannot have more than 25 cultures.");

        // Act
        var message = SafeError.LogAndResolve(logger, exception, Fallback, "adding locale 'de' to data list 7");

        // Assert
        message.Should().Be("A data list cannot have more than 25 cultures.");
        logger.Level.Should().Be(LogLevel.Information);
        logger.Exception.Should().BeNull("an expected rejection is not an error report");
    }

    [Fact]
    public void LogAndResolve_WithUnexpectedException_LogsErrorWithTheExceptionAndReturnsTheFallback()
    {
        // Arrange
        RecordingLogger logger = new();
        InvalidOperationException exception = new("Npgsql: 28P01 password authentication failed");

        // Act
        var message = SafeError.LogAndResolve(logger, exception, Fallback, "adding locale 'de' to data list 7");

        // Assert
        message.Should().Be(Fallback);
        message.Should().NotContain("Npgsql");
        logger.Level.Should().Be(LogLevel.Error);
        logger.Exception.Should().BeSameAs(exception, "the diagnostic must survive somewhere");
    }

    private sealed class RecordingLogger : ILogger
    {
        public LogLevel? Level { get; private set; }

        public Exception? Exception { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Level = logLevel;
            Exception = exception;
        }
    }
}
