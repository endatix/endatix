using Endatix.Hosting.Builders;
using Endatix.Hosting.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Endatix.Hosting.Tests.Telemetry;

/// <summary>
/// Unit tests for <see cref="EndatixTelemetryBuilder"/> resolution rules: env-var precedence,
/// off-by-default behaviour and fail-fast validation.
/// </summary>
[Collection(TelemetryEnvironmentCollection.Name)]
public sealed class EndatixTelemetryBuilderTests
{
    private const string CollectorEndpoint = "http://collector:4317";

    private static EndatixTelemetryBuilder CreateBuilder(Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? [])
            .Build();

        var services = new ServiceCollection();
        var parent = new EndatixBuilder(services, configuration);
        return parent.Telemetry;
    }

    /// <summary>
    /// Clears every OTEL_* variable this suite reads, so an ambient value on the developer's
    /// machine or the CI runner cannot change the outcome.
    /// </summary>
    private static EnvironmentVariableScope ClearOtelEnvironment(params (string Name, string? Value)[] overrides)
    {
        var variables = new List<(string, string?)>
        {
            (EndatixTelemetryBuilder.EnvVars.ServiceName, null),
            (EndatixTelemetryBuilder.EnvVars.ServiceVersion, null),
            (EndatixTelemetryBuilder.EnvVars.TracesSampler, null)
        };

        // Includes the signal-specific endpoint and protocol variables, any one of which would
        // otherwise activate telemetry from the ambient environment and skew every assertion here.
        variables.AddRange(EndatixTelemetryBuilder.EnvVars.AllOtlpEndpoints.Select(n => (n, (string?)null)));
        variables.AddRange(EndatixTelemetryBuilder.EnvVars.AllOtlpProtocols.Select(n => (n, (string?)null)));

        variables.AddRange(overrides.Select(o => (o.Name, o.Value)));
        return new EnvironmentVariableScope([.. variables]);
    }

    [Fact]
    public void UseDefaults_NoConfiguration_RegistersNoExporter()
    {
        // Arrange
        using var _ = ClearOtelEnvironment();
        var builder = CreateBuilder();

        // Act
        builder.UseDefaults().Build();

        // Assert: nothing configured means no OpenTelemetry service is registered at all
        builder.Services.Should().NotContain(
            s => s.ServiceType.FullName!.Contains("OpenTelemetry"),
            "no exporter should be allocated when telemetry is unconfigured");
    }

    [Fact]
    public void UseDefaults_WithOtlpEndpointEnvVar_RegistersOtlpExporter()
    {
        // Arrange
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, CollectorEndpoint));
        var builder = CreateBuilder();

        // Act
        builder.UseDefaults().Build();

        // Assert
        builder.Services.Should().Contain(
            s => s.ServiceType.FullName!.Contains("OpenTelemetry"),
            "an OTLP endpoint in the environment is all it should take to enable telemetry");
    }

    [Fact]
    public void ResolveOtlpEndpoint_EndpointFromConfigurationOnly_IsUsedAsFallback()
    {
        // Arrange
        using var _ = ClearOtelEnvironment();
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Endatix:Telemetry:Otlp:Endpoint"] = CollectorEndpoint
        });

        // Act
        builder.UseDefaults();
        var endpoint = builder.ResolveOtlpEndpoint();

        // Assert
        endpoint.Should().NotBeNull();
        endpoint!.ToString().Should().Be("http://collector:4317/");
    }

    [Fact]
    public void ResolveOtlpEndpoint_SetInEnvAndConfig_EnvWins()
    {
        // Arrange
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, "http://from-env:4317"));
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Endatix:Telemetry:Otlp:Endpoint"] = "http://from-config:4317"
        });

        // Act
        builder.UseDefaults();
        var endpoint = builder.ResolveOtlpEndpoint();

        // Assert
        endpoint!.Host.Should().Be("from-env");
    }

    [Fact]
    public void BuildResource_ServiceNameFromEnvAndConfig_EnvWins()
    {
        // Arrange
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.ServiceName, "endatix-api-from-env"));
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Endatix:Telemetry:ServiceName"] = "endatix-api-from-config"
        });

        // Act
        builder.UseDefaults();
        var resource = builder.BuildResource();

        // Assert
        resource["service.name"].Should().Be("endatix-api-from-env");
    }

    [Fact]
    public void BuildResource_ServiceNameFromConfigurationOnly_IsUsedAsFallback()
    {
        // Arrange
        using var _ = ClearOtelEnvironment();
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Endatix:Telemetry:ServiceName"] = "endatix-api-from-config"
        });

        // Act
        builder.UseDefaults();
        var resource = builder.BuildResource();

        // Assert
        resource["service.name"].Should().Be("endatix-api-from-config");
    }

    [Fact]
    public void BuildResource_WithConfiguredResourceAttributes_IncludesThem()
    {
        // Arrange
        using var _ = ClearOtelEnvironment();
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Endatix:Telemetry:ServiceName"] = "endatix-api",
            ["Endatix:Telemetry:ResourceAttributes:deployment.environment"] = "production"
        });

        // Act
        builder.UseDefaults();
        var resource = builder.BuildResource();

        // Assert
        resource.Should().ContainKey("deployment.environment")
            .WhoseValue.Should().Be("production");
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("collector:4317")]
    [InlineData("ftp://collector:4317")]
    public void Build_InvalidOtlpEndpoint_ThrowsWithActionableMessage(string endpoint)
    {
        // Arrange: a malformed endpoint must fail loudly, not silently stop exporting
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, endpoint));
        var builder = CreateBuilder();

        // Act
        var act = () => builder.UseDefaults().Build();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{endpoint}*")
            .WithMessage($"*{EndatixTelemetryBuilder.EnvVars.OtlpEndpoint}*");
    }

    [Fact]
    public void Build_InvalidOtlpProtocol_ThrowsWithActionableMessage()
    {
        // Arrange
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, CollectorEndpoint),
            (EndatixTelemetryBuilder.EnvVars.OtlpProtocol, "carrier-pigeon"));
        var builder = CreateBuilder();

        // Act
        var act = () => builder.UseDefaults().Build();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*carrier-pigeon*");
    }

    [Fact]
    public void Build_InvalidOtlpProtocolFromConfiguration_ThrowsNamingTheConfigKey()
    {
        // Arrange — the config fallback must fail as loudly as the environment variable does
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, CollectorEndpoint));
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Endatix:Telemetry:Otlp:Protocol"] = "smoke-signals"
        });

        // Act
        var act = () => builder.UseDefaults().Build();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*smoke-signals*")
            .WithMessage("*Endatix:Telemetry:Otlp:Protocol*");
    }

    [Theory]
    [InlineData("grpc", OtlpExportProtocol.Grpc)]
    [InlineData("http/protobuf", OtlpExportProtocol.HttpProtobuf)]
    [InlineData("  GRPC  ", OtlpExportProtocol.Grpc)]
    public void ResolveOtlpProtocol_SupportedValues_AreParsed(string configured, OtlpExportProtocol expected)
    {
        // Arrange
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpProtocol, configured));
        var builder = CreateBuilder();

        // Act
        builder.UseDefaults();
        var protocol = builder.ResolveOtlpProtocol();

        // Assert
        protocol.Should().Be(expected);
    }

    [Fact]
    public void ResolveOtlpProtocol_NotConfigured_ReturnsNullSoTheSdkDefaultApplies()
    {
        // Arrange
        using var _ = ClearOtelEnvironment();
        var builder = CreateBuilder();

        // Act
        builder.UseDefaults();

        // Assert
        builder.ResolveOtlpProtocol().Should().BeNull();
    }

    [Theory]
    [InlineData("-0.5")]
    [InlineData("1.5")]
    public void Build_SamplingRatioOutOfRange_Throws(string ratio)
    {
        // Arrange
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, CollectorEndpoint));
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Endatix:Telemetry:Traces:SamplingRatio"] = ratio
        });

        // Act
        var act = () => builder.UseDefaults().Build();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*SamplingRatio*");
    }

    [Fact]
    public void Build_CalledTwice_RegistersOnlyOnce()
    {
        // Arrange — FinalizeConfiguration() calls Build(), and a consumer may call it too
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, CollectorEndpoint));
        var builder = CreateBuilder();

        // Act
        builder.UseDefaults().Build();
        var afterFirstBuild = builder.Services.Count;
        builder.Build();

        // Assert
        builder.Services.Count.Should().Be(afterFirstBuild);
    }

    [Fact]
    public void DisableInstrumentation_CalledAfterUseDefaults_StillTakesEffect()
    {
        // Arrange — registration is deferred to Build() precisely so call order does not matter
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, CollectorEndpoint));
        var builder = CreateBuilder();

        // Act
        var act = () => builder
            .UseDefaults()
            .DisableInstrumentation(Instrumentations.Runtime | Instrumentations.HttpClient)
            .Build();

        // Assert
        act.Should().NotThrow();
        builder.Services.Should().Contain(s => s.ServiceType.FullName!.Contains("OpenTelemetry"));
    }

    [Fact]
    public void WithOtlpExporter_ExplicitEndpoint_OverridesConfiguration()
    {
        // Arrange
        using var _ = ClearOtelEnvironment();
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Endatix:Telemetry:Otlp:Endpoint"] = "http://from-config:4317"
        });

        // Act
        builder.UseDefaults().WithOtlpExporter("http://explicit:4317");
        var endpoint = builder.ResolveOtlpEndpoint();

        // Assert
        endpoint!.Host.Should().Be("explicit");
    }

    [Fact]
    public void WithOtlpExporter_WithoutUseDefaults_EnablesTelemetry()
    {
        // Arrange
        using var _ = ClearOtelEnvironment();
        var builder = CreateBuilder();

        // Act
        builder.WithOtlpExporter(CollectorEndpoint).Build();

        // Assert
        builder.Services.Should().Contain(s => s.ServiceType.FullName!.Contains("OpenTelemetry"));
    }

    [Fact]
    public void Build_WithoutUseDefaults_RegistersNothing()
    {
        // Arrange — telemetry is opt-in; FinalizeConfiguration() calling Build() must be inert
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, CollectorEndpoint));
        var builder = CreateBuilder();

        // Act
        builder.Build();

        // Assert
        builder.Services.Should().NotContain(s => s.ServiceType.FullName!.Contains("OpenTelemetry"));
    }

    [Theory]
    [InlineData("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT")]
    [InlineData("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT")]
    public void UseDefaults_WithSignalSpecificEndpointOnly_EnablesTelemetry(string variable)
    {
        // Arrange — the spec allows configuring only a signal-specific endpoint, with no global one
        using var _ = ClearOtelEnvironment((variable, CollectorEndpoint));
        var builder = CreateBuilder();

        // Act
        builder.UseDefaults().Build();

        // Assert
        builder.Services.Should().Contain(
            s => s.ServiceType.FullName!.Contains("OpenTelemetry"),
            "a signal-specific endpoint configures telemetry just as the global one does");
    }

    [Fact]
    public void ResolveOtlpEndpoint_SignalSpecificAndGlobalSet_SignalSpecificWins()
    {
        // Arrange
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, "http://global:4317"),
            (EndatixTelemetryBuilder.EnvVars.OtlpTracesEndpoint, "http://traces:4318"));
        var builder = CreateBuilder();

        // Act
        builder.UseDefaults();

        // Assert
        builder.ResolveOtlpEndpoint()!.Host.Should().Be("traces");
    }

    [Fact]
    public void BuildResource_ConfiguredAttributeForServiceName_DoesNotOverrideTheEnvironment()
    {
        // Arrange, by the back door: ResourceAttributes must not defeat OTEL_SERVICE_NAME
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.ServiceName, "endatix-api-from-env"));
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Endatix:Telemetry:ResourceAttributes:service.name"] = "hijacked",
            ["Endatix:Telemetry:ResourceAttributes:deployment.environment"] = "production"
        });

        // Act
        builder.UseDefaults();
        var resource = builder.BuildResource();

        // Assert
        resource["service.name"].Should().Be("endatix-api-from-env");
        resource["deployment.environment"].Should().Be("production");
    }

    [Fact]
    public void Build_WhenValidationFails_DoesNotMarkItselfApplied()
    {
        // Arrange — a failed Build must not latch, or FinalizeConfiguration() calling Build() after
        // a caught exception would return quietly having registered nothing
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, "not-a-uri"));
        var builder = CreateBuilder();
        builder.UseDefaults();

        // Act
        var first = () => builder.Build();
        first.Should().Throw<InvalidOperationException>();
        var second = () => builder.Build();

        // Assert
        second.Should().Throw<InvalidOperationException>(
            "the failure must resurface rather than be swallowed by the idempotency guard");
    }

    [Theory]
    [InlineData("http://collector:4317", "http://collector:4317")]
    [InlineData("https://user:s3cr3t@collector:4318/v1/traces?token=abc123#frag", "https://collector:4318")]
    [InlineData("https://bearer-token@collector.example.com/v1/metrics?api-key=deadbeef",
        "https://collector.example.com")]
    public void Redact_StripsCredentialsPathAndQuery(string endpoint, string expected)
    {
        // Arrange / Act
        var redacted = EndatixTelemetryBuilder.Redact(new Uri(endpoint));

        // Assert
        redacted.Should().Be(expected);
    }

    [Fact]
    public void Redact_NeverLeaksUserInfo()
    {
        // Arrange — GetLeftPart(UriPartial.Authority) keeps user info, unlike the Authority
        // property. This test exists so nobody "simplifies" the implementation back to it.
        var endpoint = new Uri("https://user:s3cr3t@collector:4318/v1/traces?token=abc123");

        // Act
        var redacted = EndatixTelemetryBuilder.Redact(endpoint);

        // Assert
        redacted.Should().NotContain("s3cr3t").And.NotContain("user").And.NotContain("abc123");
    }

    // The AddOtlpExporter(...) callback is a deferred options action: it does not run until the
    // provider is actually built. Every other test in this class stops at registration, so a fault
    // inside that callback -- such as assigning a null Endpoint, which OtlpExporterOptions rejects
    // with ArgumentNullException -- goes unseen and only surfaces at host startup. These tests build
    // the provider so the callback executes.

    [Theory]
    [InlineData("OTEL_EXPORTER_OTLP_ENDPOINT")]
    [InlineData("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT")]
    [InlineData("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT")]
    public void Build_EndpointFromEnvironment_ProvidersResolveWithoutThrowing(string variable)
    {
        // Arrange
        using var _ = ClearOtelEnvironment((variable, CollectorEndpoint));
        var builder = CreateBuilder();
        builder.UseDefaults().Build();

        // Act
        using var provider = builder.Services.BuildServiceProvider();
        var meters = () => provider.GetRequiredService<MeterProvider>();
        var tracers = () => provider.GetRequiredService<TracerProvider>();

        // Assert
        meters.Should().NotThrow("the SDK reads the endpoint from the environment itself");
        tracers.Should().NotThrow("the SDK reads the endpoint from the environment itself");
    }

    [Fact]
    public void Build_EndpointFromConfiguration_ProvidersResolveWithoutThrowing()
    {
        // Arrange — the other branch: the endpoint IS assigned, because the SDK cannot see it
        using var _ = ClearOtelEnvironment();
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Endatix:Telemetry:Otlp:Endpoint"] = CollectorEndpoint,
            ["Endatix:Telemetry:Otlp:Protocol"] = "http/protobuf"
        });
        builder.UseDefaults().Build();

        // Act
        using var provider = builder.Services.BuildServiceProvider();
        var meters = () => provider.GetRequiredService<MeterProvider>();
        var tracers = () => provider.GetRequiredService<TracerProvider>();

        // Assert
        meters.Should().NotThrow();
        tracers.Should().NotThrow();
    }

    [Fact]
    public void Build_WithInstrumentationDisabled_ProvidersStillResolve()
    {
        // Arrange
        using var _ = ClearOtelEnvironment(
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, CollectorEndpoint));
        var builder = CreateBuilder();
        builder.UseDefaults().DisableInstrumentation(Instrumentations.All).Build();

        // Act
        using var provider = builder.Services.BuildServiceProvider();
        var meters = () => provider.GetRequiredService<MeterProvider>();

        // Assert
        meters.Should().NotThrow();
    }

    [Fact]
    public void TelemetryOptions_SectionName_MatchesTheDocumentedKey()
    {
        // Arrange / Act / Assert — the docs and Helm chart both reference this literal
        TelemetryOptions.SectionName.Should().Be("Endatix:Telemetry");
    }
}
