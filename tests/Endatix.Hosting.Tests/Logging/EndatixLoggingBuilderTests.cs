using Endatix.Hosting.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Endatix.Hosting.Tests.Logging;

/// <summary>
/// Unit tests for <see cref="EndatixLoggingBuilder"/>: the provider baseline it guarantees and the
/// consumer hook that has to survive its own <c>ClearProviders()</c>.
/// </summary>
public sealed class EndatixLoggingBuilderTests
{
    private sealed class MarkerLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public void Dispose() { }
    }

    private sealed class SecondMarkerLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public void Dispose() { }
    }

    private static EndatixLoggingBuilder CreateBuilder(Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? [])
            .Build();

        return new EndatixLoggingBuilder(new ServiceCollection(), configuration);
    }

    [Fact]
    public void UseDefaults_Always_RegistersConsoleProvider()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        builder.UseDefaults();

        // Assert
        // The console provider is what `kubectl logs` and `docker logs` read. Losing it is the
        // regression that leaves a deployed pod producing no output at all.
        using var provider = builder.Services.BuildServiceProvider();
        var providers = provider.GetServices<ILoggerProvider>();

        providers.Should().ContainSingle(p => p is ConsoleLoggerProvider);
    }

    [Fact]
    public void UseDefaults_Always_ClearsTheProvidersTheHostAlreadyAdded()
    {
        // Arrange
        // WebApplication.CreateBuilder adds Console, Debug, EventSource and EventLog before Endatix
        // runs. Without ClearProviders() the console output is emitted twice.
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(new MarkerLoggerProvider()));

        var configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var builder = new EndatixLoggingBuilder(services, configuration);

        // Act
        builder.UseDefaults();

        // Assert
        using var provider = services.BuildServiceProvider();
        var providers = provider.GetServices<ILoggerProvider>();

        providers.Should().NotContain(p => p is MarkerLoggerProvider);
    }

    [Fact]
    public void Configure_AfterUseDefaults_StillAppliesTheProvider()
    {
        // Arrange
        // The ordering trap this hook exists for: AddLogging runs its callback immediately, so a
        // consumer provider registered before the baseline would be erased by its ClearProviders().
        var builder = CreateBuilder();
        builder.UseDefaults();

        // Act
        builder.Configure(logging => logging.AddProvider(new MarkerLoggerProvider()));

        // Assert
        using var provider = builder.Services.BuildServiceProvider();
        var providers = provider.GetServices<ILoggerProvider>();

        providers.Should().Contain(p => p is MarkerLoggerProvider);
        providers.Should().Contain(p => p is ConsoleLoggerProvider);
    }

    [Fact]
    public void Configure_CalledTwice_AppliesBoth()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        builder
            .Configure(logging => logging.AddProvider(new MarkerLoggerProvider()))
            .Configure(logging => logging.AddProvider(new SecondMarkerLoggerProvider()));

        // Assert
        using var provider = builder.Services.BuildServiceProvider();
        var providers = provider.GetServices<ILoggerProvider>().ToList();

        providers.Should().Contain(p => p is MarkerLoggerProvider);
        providers.Should().Contain(p => p is SecondMarkerLoggerProvider);

        // Call order is preserved, which is what makes the hook predictable when two consumers
        // both add a provider that writes to the same sink.
        var first = providers.FindIndex(p => p is MarkerLoggerProvider);
        var second = providers.FindIndex(p => p is SecondMarkerLoggerProvider);
        first.Should().BeLessThan(second);
    }

    [Fact]
    public void Configure_WithoutUseDefaults_StillRegistersTheConsoleBaseline()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        builder.Configure(logging => logging.AddProvider(new MarkerLoggerProvider()));

        // Assert
        using var provider = builder.Services.BuildServiceProvider();
        var providers = provider.GetServices<ILoggerProvider>();

        providers.Should().Contain(p => p is ConsoleLoggerProvider);
        providers.Should().Contain(p => p is MarkerLoggerProvider);
    }

    [Fact]
    public void Configure_WithNullAction_Throws()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var act = () => builder.Configure(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UseDefaults_WithLegacySerilogSection_StartsAndKeepsLogging()
    {
        // Arrange
        // A host upgrading from the Serilog configuration must not fail to start, and must not fall
        // through to no logging at all -- the section is simply no longer read.
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["Serilog:WriteTo:0:Name"] = "Console"
        });

        // Act
        var act = () => builder.UseDefaults();

        // Assert
        act.Should().NotThrow();

        using var provider = builder.Services.BuildServiceProvider();
        provider.GetServices<ILoggerProvider>().Should().Contain(p => p is ConsoleLoggerProvider);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UseApplicationInsights_ThenUseDefaults_KeepsTheApplicationInsightsProvider(bool withCustomOptions)
    {
        // Arrange
        // AddApplicationInsightsTelemetry registers an ILoggerProvider. Registering it before the
        // Endatix baseline would put it in front of ClearProviders(), which silently drops it --
        // the host keeps starting, and only the missing telemetry gives it away.
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["ApplicationInsights:ConnectionString"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000"
        });

        // Act
        if (withCustomOptions)
        {
            builder.UseApplicationInsights(options => options.EnableAdaptiveSampling = false);
        }
        else
        {
            builder.UseApplicationInsights();
        }

        builder.UseDefaults();

        // Assert
        // Asserted on the descriptors rather than resolved instances: Application Insights needs
        // IHostingEnvironment to activate, which a bare ServiceCollection has no reason to carry.
        // Survival of the registration is the claim under test either way.
        var loggerProviders = builder.Services
            .Where(d => d.ServiceType == typeof(ILoggerProvider))
            .ToList();

        loggerProviders.Should().Contain(d =>
            d.ImplementationType != null && d.ImplementationType.Name.Contains("ApplicationInsights"));
        loggerProviders.Should().Contain(d => d.ImplementationType == typeof(ConsoleLoggerProvider));
    }

    [Fact]
    public void UseDefaults_CalledTwice_RegistersConsoleOnlyOnce()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        builder.UseDefaults();
        builder.UseDefaults();

        // Assert
        using var provider = builder.Services.BuildServiceProvider();

        provider.GetServices<ILoggerProvider>()
            .Count(p => p is ConsoleLoggerProvider)
            .Should().Be(1);
    }
}
