using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class JobTypePoliciesTests
{
    private const string WebHook = "WebHookDelivery";

    [Fact]
    public void For_NoConfiguration_UsesTheDeclaredDefaults()
    {
        // Arrange
        var policies = Policies(new ConfigurationBuilder().Build(), DeclaredDefaults.For(DeclaredDefaults.Tuned, WebHook));

        // Act
        var policy = policies.For(WebHook);

        // Assert
        policy.Should().Be(new BackgroundJobTypePolicy(
            MaxAttempts: 8,
            MaxRuntime: TimeSpan.FromMinutes(5),
            BackoffBase: TimeSpan.FromSeconds(10),
            BackoffCap: TimeSpan.FromSeconds(3600),
            MaxConcurrency: 4,
            Retention: TimeSpan.FromDays(3)));
    }

    [Fact]
    public void For_JobTypeConfigured_OverridesTheDeclaredDefaultPerKey()
    {
        // Arrange
        var configuration = InMemory(new() { [$"Endatix:BackgroundJobs:JobTypes:{WebHook}:MaxAttempts"] = "10" });
        var policies = Policies(configuration, DeclaredDefaults.For(DeclaredDefaults.Tuned, WebHook));

        // Act
        var policy = policies.For(WebHook);

        // Assert — the override replaces only the key it sets.
        policy.MaxAttempts.Should().Be(10);
        policy.BackoffBase.Should().Be(TimeSpan.FromSeconds(10));
        policy.MaxConcurrency.Should().Be(4);
    }

    [Fact]
    public void For_JobTypeAndGlobalConfigured_TakesTheJobTypeKey()
    {
        // Arrange
        var configuration = InMemory(new()
        {
            [$"Endatix:BackgroundJobs:JobTypes:{WebHook}:MaxAttempts"] = "10",
            ["Endatix:BackgroundJobs:MaxAttempts"] = "6",
        });
        var policies = Policies(configuration, DeclaredDefaults.For(DeclaredDefaults.Tuned, WebHook));

        // Act
        var policy = policies.For(WebHook);

        // Assert
        policy.MaxAttempts.Should().Be(10);
    }

    [Fact]
    public void For_JobTypeConfiguredThroughEnvironmentVariables_OverridesTheDeclaredDefault()
    {
        // Arrange
        using var environment = new EnvironmentVariables(
            ($"Endatix__BackgroundJobs__JobTypes__{WebHook}__MaxConcurrency", "6"));
        var policies = Policies(environment.Configuration, DeclaredDefaults.For(DeclaredDefaults.Tuned, WebHook));

        // Act
        var policy = policies.For(WebHook);

        // Assert
        policy.MaxConcurrency.Should().Be(6);
        policy.MaxAttempts.Should().Be(8);
    }

    [Fact]
    public void For_GlobalConfigured_OverridesTheDeclaredDefault()
    {
        // Arrange
        var configuration = InMemory(new() { ["Endatix:BackgroundJobs:RetentionDays"] = "30" });
        var policies = Policies(configuration, DeclaredDefaults.For(DeclaredDefaults.Tuned, WebHook));

        // Act
        var declared = policies.For(WebHook);
        var undeclared = policies.For("SubmissionExport");

        // Assert — only the key the host set changes; every other setting keeps the declared default.
        declared.Retention.Should().Be(TimeSpan.FromDays(30));
        declared.MaxAttempts.Should().Be(8);
        undeclared.Retention.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void For_GlobalConfiguredThroughEnvironmentVariables_OverridesTheDeclaredDefault()
    {
        // Arrange
        using var environment = new EnvironmentVariables(("Endatix__BackgroundJobs__RetentionDays", "30"));
        var policies = Policies(environment.Configuration, DeclaredDefaults.For(DeclaredDefaults.Tuned, WebHook));

        // Act
        var policy = policies.For(WebHook);

        // Assert
        policy.Retention.Should().Be(TimeSpan.FromDays(30));
        policy.MaxAttempts.Should().Be(8);
    }

    [Fact]
    public void For_GlobalAssignedInCode_OverridesTheDeclaredDefault()
    {
        // Arrange — a host that sets the option in code configures it as much as one that sets the key.
        var options = new BackgroundJobsOptions { MaxAttempts = 6 };
        var policies = new JobTypePolicies(Options.Create(options), DeclaredDefaults.For(DeclaredDefaults.Tuned, WebHook));

        // Act
        var policy = policies.For(WebHook);

        // Assert
        policy.MaxAttempts.Should().Be(6);
    }

    [Fact]
    public void For_DeclaredDefaultLeavesKeyUnset_FallsBackToTheConfiguredGlobal()
    {
        // Arrange
        var configuration = InMemory(new() { ["Endatix:BackgroundJobs:BackoffBaseSeconds"] = "20" });
        var policies = Policies(
            configuration,
            DeclaredDefaults.For(new BackgroundJobTypeDefaults { MaxAttempts = 8 }, WebHook));

        // Act
        var policy = policies.For(WebHook);

        // Assert
        policy.MaxAttempts.Should().Be(8);
        policy.BackoffBase.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void For_NothingConfiguredOrDeclared_UsesTheGlobalDefaults()
    {
        // Arrange
        var policies = Policies(new ConfigurationBuilder().Build(), DeclaredDefaults.None);

        // Act
        var policy = policies.For("SubmissionExport");

        // Assert
        policy.Should().Be(new BackgroundJobTypePolicy(
            MaxAttempts: 3,
            MaxRuntime: TimeSpan.FromMinutes(60),
            BackoffBase: TimeSpan.FromSeconds(30),
            BackoffCap: TimeSpan.FromSeconds(900),
            MaxConcurrency: 1,
            Retention: TimeSpan.FromDays(7)));
    }

    private static JobTypePolicies Policies(IConfiguration configuration, JobTypeDefaults declared)
    {
        var options = new BackgroundJobsOptions();
        configuration.GetSection(BackgroundJobsOptions.SectionName).Bind(options);
        return new JobTypePolicies(Options.Create(options), declared);
    }

    private static IConfiguration InMemory(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    /// <summary>
    /// Sets environment variables under a prefix of the test's own, so no other test reads them, and removes them
    /// when disposed.
    /// </summary>
    private sealed class EnvironmentVariables : IDisposable
    {
        private readonly string _prefix = $"JOBTYPEPOLICIESTESTS_{Guid.NewGuid():N}_";
        private readonly string[] _names;

        public EnvironmentVariables(params (string Name, string Value)[] variables)
        {
            _names = variables.Select(variable => _prefix + variable.Name).ToArray();
            foreach (var (name, value) in variables)
            {
                Environment.SetEnvironmentVariable(_prefix + name, value);
            }

            Configuration = new ConfigurationBuilder().AddEnvironmentVariables(_prefix).Build();
        }

        public IConfiguration Configuration { get; }

        public void Dispose()
        {
            foreach (var name in _names)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }
    }
}
