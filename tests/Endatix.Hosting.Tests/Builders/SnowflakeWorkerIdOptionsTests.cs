using System.Reflection;
using Endatix.Hosting.Builders;
using Endatix.Infrastructure.Builders;
using Endatix.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Hosting.Tests.Builders;

public class SnowflakeWorkerIdOptionsTests
{
    private static readonly Dictionary<string, string?> _securityConfig = new()
    {
        ["Endatix:Auth:DefaultScheme"] = InfrastructureSecurityBuilder.MULTI_JWT_SCHEME_NAME,
        ["Endatix:Auth:Providers:EndatixJwt:Issuer"] = "test",
        ["Endatix:Auth:Providers:EndatixJwt:SigningKey"] = "test-signing-key-32-characters-long",
        ["Endatix:Auth:Providers:EndatixJwt:Audiences:0"] = "test",
        ["Endatix:Auth:Providers:EndatixJwt:Enabled"] = "true"
    };

    [Theory]
    [InlineData("-1")]
    [InlineData("1024")]
    public void SnowflakeWorkerId_OutOfRange_FailsAtStartup(string value)
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            ["Endatix:Data:SnowflakeWorkerId"] = value
        });

        var act = () => ValidateOnStart(provider);

        act.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    public void SnowflakeWorkerId_NotAnInteger_FailsAtStartup(string value)
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            ["Endatix:Data:SnowflakeWorkerId"] = value
        });

        var act = () => ValidateOnStart(provider);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1023", 1023)]
    public void SnowflakeWorkerId_InRange_IsAccepted(string value, int expected)
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            ["Endatix:Data:SnowflakeWorkerId"] = value
        });

        var act = () => ValidateOnStart(provider);

        act.Should().NotThrow();
        provider.GetRequiredService<IOptions<DataOptions>>().Value.SnowflakeWorkerId.Should().Be(expected);
    }

    [Fact]
    public void UseSnowflakeWorkerId_WhenConfigurationIsAbsent_AppliesBuilderValue()
    {
        var (services, warnings) = Compose(configuration: null, builderWorkerId: 4);

        using var provider = services.BuildServiceProvider();
        var workerId = provider.GetRequiredService<IOptions<DataOptions>>().Value.SnowflakeWorkerId;

        workerId.Should().Be(4);
        warnings.Should().BeEmpty();
    }

    [Fact]
    public void UseSnowflakeWorkerId_WhenConfigurationIsPresent_UsesConfiguration()
    {
        var (services, warnings) = Compose(
            new Dictionary<string, string?> { ["Endatix:Data:SnowflakeWorkerId"] = "9" },
            builderWorkerId: null);

        using var provider = services.BuildServiceProvider();
        var workerId = provider.GetRequiredService<IOptions<DataOptions>>().Value.SnowflakeWorkerId;

        workerId.Should().Be(9);
        warnings.Should().BeEmpty();
    }

    [Fact]
    public void UseSnowflakeWorkerId_WhenConfigurationDiffers_LogsOneWarningAndUsesConfiguration()
    {
        var (services, warnings) = Compose(
            new Dictionary<string, string?> { ["Endatix:Data:SnowflakeWorkerId"] = "5" },
            builderWorkerId: 3);

        using var provider = services.BuildServiceProvider();
        var workerId = provider.GetRequiredService<IOptions<DataOptions>>().Value.SnowflakeWorkerId;

        workerId.Should().Be(5);
        warnings.Should().ContainSingle()
            .Which.Should().Contain("Endatix:Data:SnowflakeWorkerId")
            .And.Contain("configuration wins")
            .And.Contain("5")
            .And.Contain("3");
    }

    [Fact]
    public void UseSnowflakeWorkerId_WhenOptionsAreBuiltMoreThanOnce_LogsOneWarning()
    {
        var (services, warnings) = Compose(
            new Dictionary<string, string?> { ["Endatix:Data:SnowflakeWorkerId"] = "5" },
            builderWorkerId: 3);

        using var provider = services.BuildServiceProvider();
        ValidateOnStart(provider);
        _ = provider.GetRequiredService<IOptions<DataOptions>>().Value.SnowflakeWorkerId;
        _ = provider.GetRequiredService<IOptionsMonitor<DataOptions>>().CurrentValue.SnowflakeWorkerId;

        warnings.Should().ContainSingle();
    }

    [Fact]
    public void UseSnowflakeWorkerId_WhenConfigurationMatches_LogsNoWarning()
    {
        var (services, warnings) = Compose(
            new Dictionary<string, string?> { ["Endatix:Data:SnowflakeWorkerId"] = "3" },
            builderWorkerId: 3);

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IOptions<DataOptions>>().Value.SnowflakeWorkerId;

        warnings.Should().BeEmpty();
    }

    [Fact]
    public void FinalizeConfiguration_WithoutPersistenceBuild_AppliesConfiguredWorkerId()
    {
        using var provider = Finalize(
            new Dictionary<string, string?> { ["Endatix:Data:SnowflakeWorkerId"] = "7" },
            builderWorkerId: null);

        provider.GetRequiredService<IOptions<DataOptions>>().Value.SnowflakeWorkerId.Should().Be(7);
    }

    [Fact]
    public void FinalizeConfiguration_WithoutPersistenceBuild_AppliesBuilderWorkerId()
    {
        using var provider = Finalize(configuration: null, builderWorkerId: 5);

        provider.GetRequiredService<IOptions<DataOptions>>().Value.SnowflakeWorkerId.Should().Be(5);
    }

    [Fact]
    public void FinalizeConfiguration_WithoutBuilderValueOrKey_KeepsHostConfiguredWorkerId()
    {
        using var provider = Finalize(
            configuration: null,
            builderWorkerId: null,
            builder => builder.Infrastructure.Data.Configure(options => options.SnowflakeWorkerId = 8));

        provider.GetRequiredService<IOptions<DataOptions>>().Value.SnowflakeWorkerId.Should().Be(8);
    }

    [Fact]
    public void FinalizeConfiguration_WhenHostConfigureRunsAfterBinding_ConfigurationStillWins()
    {
        using var provider = Finalize(
            new Dictionary<string, string?> { ["Endatix:Data:SnowflakeWorkerId"] = "5" },
            builderWorkerId: null,
            builder =>
            {
                builder.Persistence.Build();
                builder.Infrastructure.Data.Configure(options => options.SnowflakeWorkerId = 8);
            });

        provider.GetRequiredService<IOptions<DataOptions>>().Value.SnowflakeWorkerId.Should().Be(5);
    }

    [Fact]
    public void FinalizeConfiguration_WhenHostConfigureRunsAfterBinding_BuilderValueStillFillsAbsentKey()
    {
        using var provider = Finalize(
            configuration: null,
            builderWorkerId: 3,
            builder => builder.Persistence.Build());

        provider.GetRequiredService<IOptions<DataOptions>>().Value.SnowflakeWorkerId.Should().Be(3);
    }

    [Fact]
    public void FinalizeConfiguration_WhenHostConfigureRunsAfterBinding_NonIntegerConfigurationStillFailsAtStartup()
    {
        using var provider = Finalize(
            new Dictionary<string, string?> { ["Endatix:Data:SnowflakeWorkerId"] = "abc" },
            builderWorkerId: null,
            builder => builder.Infrastructure.Data.Configure(options => options.SnowflakeWorkerId = 8));

        var act = () => ValidateOnStart(provider);

        act.Should().Throw<InvalidOperationException>();
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var (services, _) = Compose(settings, builderWorkerId: null);
        return services.BuildServiceProvider();
    }

    private static (ServiceCollection Services, List<string> Warnings) Compose(
        Dictionary<string, string?>? configuration,
        int? builderWorkerId)
    {
        var services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(configuration).Build();
        services.AddSingleton(config);
        var builder = new EndatixBuilder(services, config);
        var logs = new List<string>();
        AddLogger(builder.LoggerFactory, new WarningLoggerProvider(logs));

        if (builderWorkerId is int workerId)
        {
            builder.Persistence.UseSnowflakeWorkerId(workerId);
        }

        builder.Persistence.Build();
        return (services, logs);
    }

    private static ServiceProvider Finalize(
        Dictionary<string, string?>? configuration,
        int? builderWorkerId,
        Action<EndatixBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(_securityConfig)
            .AddInMemoryCollection(configuration)
            .Build();
        services.AddSingleton(config);
        var builder = new EndatixBuilder(services, config);
        builder.Infrastructure.Security.UseDefaults();
        configure?.Invoke(builder);

        if (builderWorkerId is int workerId)
        {
            builder.UseSnowflakeWorkerId(workerId);
        }

        builder.FinalizeConfiguration();
        return services.BuildServiceProvider();
    }

    private static void AddLogger(ILoggerFactory factory, ILoggerProvider provider)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var addProvider = factory.GetType().GetMethod("AddProvider", flags, [typeof(ILoggerProvider)]);
        if (addProvider is not null)
        {
            addProvider.Invoke(factory, [provider]);
            return;
        }

        throw new InvalidOperationException($"Cannot attach a logger to {factory.GetType().FullName}.");
    }

    private static void ValidateOnStart(ServiceProvider provider)
    {
        foreach (var validator in provider.GetServices<IStartupValidator>())
        {
            validator.Validate();
        }
    }

    private sealed class WarningLoggerProvider(List<string> warnings) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new WarningLogger(warnings);

        public void Dispose()
        {
        }

        private sealed class WarningLogger(List<string> warnings) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Warning;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                {
                    warnings.Add(formatter(state, exception));
                }
            }
        }
    }
}
