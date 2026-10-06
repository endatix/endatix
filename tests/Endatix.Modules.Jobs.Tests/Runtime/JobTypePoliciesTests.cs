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
    public void For_JobTypeConfiguredThroughEnvironmentVariables_OverridesTheDeclaredDefault()
    {
        // Arrange — a prefix of the test's own, so no other test reads the variable.
        var prefix = $"JOBTYPEPOLICIESTESTS_{Guid.NewGuid():N}_";
        var variable = $"{prefix}Endatix__BackgroundJobs__JobTypes__{WebHook}__MaxConcurrency";
        Environment.SetEnvironmentVariable(variable, "6");
        try
        {
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build();
            var policies = Policies(configuration, DeclaredDefaults.For(DeclaredDefaults.Tuned, WebHook));

            // Act
            var policy = policies.For(WebHook);

            // Assert
            policy.MaxConcurrency.Should().Be(6);
            policy.MaxAttempts.Should().Be(8);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void For_GlobalConfigured_DeclaredDefaultStillApplies()
    {
        // Arrange — the global value is the setting of job types that declare none, whether it is configured or not.
        var configuration = InMemory(new() { ["Endatix:BackgroundJobs:MaxAttempts"] = "6" });
        var policies = Policies(configuration, DeclaredDefaults.For(DeclaredDefaults.Tuned, WebHook));

        // Act
        var declared = policies.For(WebHook);
        var undeclared = policies.For("SubmissionExport");

        // Assert
        declared.MaxAttempts.Should().Be(8);
        undeclared.MaxAttempts.Should().Be(6);
    }

    [Fact]
    public void For_DeclaredDefaultLeavesKeyUnset_FallsBackToTheGlobal()
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
        policy.MaxConcurrency.Should().Be(BackgroundJobsOptions.DefaultJobTypeMaxConcurrency);
    }

    private static JobTypePolicies Policies(IConfiguration configuration, JobTypeDefaults declared)
    {
        var options = new BackgroundJobsOptions();
        configuration.GetSection(BackgroundJobsOptions.SectionName).Bind(options);
        return new JobTypePolicies(Options.Create(options), declared);
    }

    private static IConfiguration InMemory(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
