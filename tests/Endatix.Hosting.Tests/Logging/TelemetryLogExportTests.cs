using System.Diagnostics;
using Endatix.Hosting.Builders;
using Endatix.Hosting.Tests.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Endatix.Hosting.Tests.Logging;

/// <summary>
/// Tests for the OTLP log signal: that it is registered only when an endpoint is resolvable, that it
/// survives the logging builder's <c>ClearProviders()</c>, and that a record carries the ambient
/// trace context a backend needs to join it to its span.
/// </summary>
[Collection(TelemetryEnvironmentCollection.Name)]
public sealed class TelemetryLogExportTests
{
    private const string CollectorEndpoint = "http://collector:4317";

    private static EnvironmentVariableScope ClearOtelEnvironment(params (string Name, string? Value)[] overrides)
    {
        var variables = new List<(string, string?)>
        {
            (EndatixTelemetryBuilder.EnvVars.ServiceName, null),
            (EndatixTelemetryBuilder.EnvVars.ServiceVersion, null),
            (EndatixTelemetryBuilder.EnvVars.TracesSampler, null)
        };

        variables.AddRange(EndatixTelemetryBuilder.EnvVars.AllOtlpEndpoints.Select(n => (n, (string?)null)));
        variables.AddRange(EndatixTelemetryBuilder.EnvVars.AllOtlpProtocols.Select(n => (n, (string?)null)));
        variables.AddRange(overrides.Select(o => (o.Name, o.Value)));

        return new EnvironmentVariableScope([.. variables]);
    }

    private static EndatixBuilder CreateHostBuilder(Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? [])
            .Build();

        return new EndatixBuilder(new ServiceCollection(), configuration);
    }

    [Fact]
    public void Build_WithOtlpEndpoint_RegistersOpenTelemetryLoggerProvider()
    {
        // Arrange
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, CollectorEndpoint));

        var builder = CreateHostBuilder();

        // Act
        builder.Telemetry.UseDefaults().Build();

        // Assert
        using var provider = builder.Services.BuildServiceProvider();

        provider.GetServices<ILoggerProvider>()
            .Should().Contain(p => p is OpenTelemetryLoggerProvider);
    }

    [Fact]
    public void Build_WithoutOtlpEndpoint_RegistersNoOpenTelemetryLoggerProvider()
    {
        // Arrange
        // Nothing configured means nothing registered: a host with no collector must not pay for an
        // exporter, and must not queue records that can never be delivered.
        using var _ = ClearOtelEnvironment();
        var builder = CreateHostBuilder();

        // Act
        builder.Telemetry.UseDefaults().Build();

        // Assert
        using var provider = builder.Services.BuildServiceProvider();

        provider.GetServices<ILoggerProvider>()
            .Should().NotContain(p => p is OpenTelemetryLoggerProvider);
    }

    [Fact]
    public void LoggingRegistersBeforeTelemetry_SoOtelProviderSurvives()
    {
        // Arrange
        // EndatixLoggingBuilder.RegisterConfiguredLogger() calls ClearProviders(), and AddLogging
        // applies its callback immediately -- so the OTel logger provider only survives because
        // Telemetry.Build() runs after Logging.UseDefaults(). This is that ordering, asserted.
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, CollectorEndpoint));

        var builder = CreateHostBuilder();

        // Act
        builder.Logging.UseDefaults();
        builder.Telemetry.UseDefaults().Build();

        // Assert
        using var provider = builder.Services.BuildServiceProvider();
        var providers = provider.GetServices<ILoggerProvider>().ToList();

        providers.Should().Contain(p => p is OpenTelemetryLoggerProvider);
        providers.Should().Contain(p => p is Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider);
    }

    [Fact]
    public void EmittedRecord_CarriesTheAmbientActivityTraceId()
    {
        // Arrange
        // Correlation is the whole point of exporting logs over OTLP: without the trace id on the
        // record, a backend cannot show the log line alongside the span it happened in.
        var exported = new List<LogRecord>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("Endatix.Tests.Correlation");
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.ClearProviders());
        services.AddOpenTelemetry().WithLogging(
            logging => logging.AddInMemoryExporter(exported),
            options => options.IncludeFormattedMessage = true);

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Endatix.Tests");

        // Act
        using var activity = source.StartActivity("unit-of-work");
        logger.LogWarning("record inside a trace");

        // Assert
        activity.Should().NotBeNull();
        exported.Should().ContainSingle();
        exported[0].TraceId.Should().Be(activity!.TraceId);
        exported[0].SpanId.Should().Be(activity.SpanId);
    }

    [Fact]
    public void EmittedRecord_RetainsStructuredPropertiesAsAttributes()
    {
        // Arrange
        // A structured log call must survive as queryable attributes, not collapse into one
        // pre-rendered string -- that is the difference between "search by form id" and grep.
        var exported = new List<LogRecord>();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.ClearProviders());
        services.AddOpenTelemetry().WithLogging(
            logging => logging.AddInMemoryExporter(exported),
            options =>
            {
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;
            });

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Endatix.Tests");

        // Act
        logger.LogWarning("Form {FormId} rejected after {Attempts} attempts", 12345L, 3);

        // Assert
        exported.Should().ContainSingle();
        var record = exported[0];

        record.FormattedMessage.Should().Be("Form 12345 rejected after 3 attempts");
        record.Attributes.Should().NotBeNull();
        record.Attributes!.Should().Contain(a => a.Key == "FormId" && Equals(a.Value, 12345L));
        record.Attributes!.Should().Contain(a => a.Key == "Attempts" && Equals(a.Value, 3));
    }
}
