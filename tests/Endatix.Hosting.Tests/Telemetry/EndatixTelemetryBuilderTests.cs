using Endatix.Hosting.Builders;
using Endatix.Hosting.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;

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
            (EndatixTelemetryBuilder.EnvVars.OtlpEndpoint, null),
            (EndatixTelemetryBuilder.EnvVars.OtlpProtocol, null),
            (EndatixTelemetryBuilder.EnvVars.TracesSampler, null)
        };

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

        // Assert — AC6: nothing configured means no OpenTelemetry service is registered at all
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
        // Arrange — AC2
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
        // Arrange — AC8: a malformed endpoint must fail loudly, not silently stop exporting
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

    [Fact]
    public void TelemetryOptions_SectionName_MatchesTheDocumentedKey()
    {
        // Arrange / Act / Assert — the docs and Helm chart both reference this literal
        TelemetryOptions.SectionName.Should().Be("Endatix:Telemetry");
    }
}
