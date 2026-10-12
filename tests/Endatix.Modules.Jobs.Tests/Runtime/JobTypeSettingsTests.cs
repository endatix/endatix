using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.Configuration;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class JobTypeSettingsTests
{
    private const string JobType = "WebHookDelivery";

    private static readonly BackgroundJobTypeDefaults Declared = new() { MaxAttempts = 8, MaxConcurrency = 4 };

    [Fact]
    public void Resolve_JobTypeKeySet_TakesItFromTheJobTypeConfiguration()
    {
        // Arrange
        var options = Bind(new()
        {
            [$"JobTypes:{JobType}:MaxAttempts"] = "10",
            ["MaxAttempts"] = "6",
        });

        // Act
        var settings = JobTypeSettings.Resolve(options, JobType, Declared);

        // Assert
        settings.MaxAttempts.Should().Be(new JobTypeSetting(
            10,
            JobTypeSettingSource.JobTypeConfiguration,
            $"Endatix:BackgroundJobs:JobTypes:{JobType}:MaxAttempts"));
    }

    [Fact]
    public void Resolve_GlobalKeySet_TakesItFromTheGlobalConfiguration()
    {
        // Arrange
        var options = Bind(new() { ["MaxAttempts"] = "6" });

        // Act
        var settings = JobTypeSettings.Resolve(options, JobType, Declared);

        // Assert
        settings.MaxAttempts.Should().Be(new JobTypeSetting(
            6,
            JobTypeSettingSource.GlobalConfiguration,
            "Endatix:BackgroundJobs:MaxAttempts"));
    }

    [Fact]
    public void Resolve_NothingConfigured_TakesTheJobTypeDefaultNamedByTheKeyThatReplacesIt()
    {
        // Arrange
        var options = Bind(new());

        // Act
        var settings = JobTypeSettings.Resolve(options, JobType, Declared);

        // Assert
        settings.MaxAttempts.Should().Be(new JobTypeSetting(
            8,
            JobTypeSettingSource.JobTypeDefault,
            $"Endatix:BackgroundJobs:JobTypes:{JobType}:MaxAttempts (the job type's default, declared in code)"));
    }

    [Fact]
    public void Resolve_NothingConfiguredOrDeclared_TakesTheGlobalDefault()
    {
        // Arrange
        var options = Bind(new());

        // Act
        var settings = JobTypeSettings.Resolve(options, JobType, declared: null);

        // Assert
        settings.MaxAttempts.Should().Be(new JobTypeSetting(
            3,
            JobTypeSettingSource.GlobalDefault,
            "Endatix:BackgroundJobs:MaxAttempts"));
    }

    [Fact]
    public void Resolve_MaxConcurrencyNothingConfiguredOrDeclared_TakesOneNamedByTheJobTypeKey()
    {
        // Arrange — there is no global MaxConcurrency key to name.
        var options = Bind(new());

        // Act
        var settings = JobTypeSettings.Resolve(options, JobType, declared: null);

        // Assert
        settings.MaxConcurrency.Should().Be(new JobTypeSetting(
            BackgroundJobsOptions.DefaultJobTypeMaxConcurrency,
            JobTypeSettingSource.GlobalDefault,
            $"Endatix:BackgroundJobs:JobTypes:{JobType}:MaxConcurrency (the default, declared in code)"));
    }

    [Fact]
    public void Resolve_GlobalMaxConcurrencyKeyWritten_IsIgnored()
    {
        // Arrange — a global MaxConcurrency key binds to nothing, so it cannot replace a job type's default.
        var options = Bind(new() { ["MaxConcurrency"] = "9" });

        // Act
        var settings = JobTypeSettings.Resolve(options, JobType, Declared);

        // Assert
        settings.MaxConcurrency.Value.Should().Be(4);
        settings.MaxConcurrency.Source.Should().Be(JobTypeSettingSource.JobTypeDefault);
    }

    private static BackgroundJobsOptions Bind(Dictionary<string, string?> section)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(section.Select(entry =>
                KeyValuePair.Create($"{BackgroundJobsOptions.SectionName}:{entry.Key}", entry.Value)))
            .Build();

        var options = new BackgroundJobsOptions();
        configuration.GetSection(BackgroundJobsOptions.SectionName).Bind(options);
        return options;
    }
}
