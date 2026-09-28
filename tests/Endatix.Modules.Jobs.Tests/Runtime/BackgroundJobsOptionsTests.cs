using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.Configuration;
using Endatix.Framework.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public class BackgroundJobsOptionsTests
{
    public static TheoryData<string, Dictionary<string, string?>, string[]> InvalidValues => new()
    {
        {
            "job type concurrency below 0",
            new() { ["JobTypes:X:MaxConcurrency"] = "-1" },
            ["JobTypes:X:MaxConcurrency"]
        },
        {
            "job type concurrency above its upper bound",
            new() { ["JobTypes:X:MaxConcurrency"] = "1001" },
            ["JobTypes:X:MaxConcurrency"]
        },
        {
            "idle wait below 1",
            new() { ["IdleWaitTimeSeconds"] = "0" },
            ["IdleWaitTimeSeconds"]
        },
        {
            "cancellation poll below 1",
            new() { ["CancellationPollSeconds"] = "0" },
            ["CancellationPollSeconds"]
        },
        {
            "check-in interval not positive",
            new() { ["Clustering:CheckinIntervalSeconds"] = "0" },
            ["Clustering:CheckinIntervalSeconds"]
        },
        {
            "retention below 1 day",
            new() { ["JobTypes:X:RetentionDays"] = "0" },
            ["JobTypes:X:RetentionDays"]
        },
        {
            "backoff cap below base",
            new() { ["BackoffBaseSeconds"] = "30", ["BackoffCapSeconds"] = "10" },
            ["BackoffCapSeconds"]
        },
        {
            "job type attempts below 1",
            new() { ["JobTypes:X:MaxAttempts"] = "0" },
            ["JobTypes:X:MaxAttempts"]
        },
        {
            // The job type overrides one side of the pair and inherits the other, so each key has to be named
            // where its value was written.
            "job type backoff cap below the inherited global base",
            new() { ["BackoffBaseSeconds"] = "120", ["JobTypes:X:BackoffCapSeconds"] = "60" },
            ["JobTypes:X:BackoffCapSeconds", "BackoffBaseSeconds"]
        },
        {
            "job type backoff base above the inherited global cap",
            new() { ["BackoffCapSeconds"] = "60", ["JobTypes:X:BackoffBaseSeconds"] = "120" },
            ["JobTypes:X:BackoffBaseSeconds", "BackoffCapSeconds"]
        },
    };

    [Fact]
    public void Bind_EmptyConfiguration_UsesDocumentedDefaults()
    {
        // Arrange
        var section = new Dictionary<string, string?>();

        // Act
        var options = Bind(section);

        // Assert
        options.RunInProcess.Should().BeTrue();
        options.IdleWaitTimeSeconds.Should().Be(2);
        options.CancellationPollSeconds.Should().Be(10);
        options.Clustering.CheckinIntervalSeconds.Should().Be(7.5);
        options.Clustering.CheckinMisfireThresholdSeconds.Should().Be(7.5);
        options.Clustering.InstanceId.Should().BeNull();
        options.RetentionDays.Should().Be(7);
        options.MaxRuntimeMinutes.Should().Be(60);
        options.MaxAttempts.Should().Be(3);
        options.BackoffBaseSeconds.Should().Be(30);
        options.BackoffCapSeconds.Should().Be(900);
    }

    [Fact]
    public void ResolvePolicy_TypeOverride_FallsBackPerKey()
    {
        // Arrange
        var options = Bind(new() { ["JobTypes:WebHookDelivery:MaxAttempts"] = "8" });

        // Act
        var overridden = options.ResolvePolicy("WebHookDelivery");
        var notOverridden = options.ResolvePolicy("SubmissionExport");

        // Assert — an override replaces only the key it sets; every other key, and every other job type, keeps the
        // global value.
        overridden.MaxAttempts.Should().Be(8);
        overridden.BackoffBase.Should().Be(TimeSpan.FromSeconds(30));
        notOverridden.MaxAttempts.Should().Be(3);
    }

    [Fact]
    public void ResolvePolicy_KeyCaseDiffers_MatchesOverride()
    {
        // Arrange
        var options = Bind(new() { ["JobTypes:webhookdelivery:MaxAttempts"] = "8" });

        // Act
        var policy = options.ResolvePolicy("WebHookDelivery");

        // Assert — configuration keys are case-insensitive everywhere else, so an override written in another case
        // must apply rather than be silently ignored.
        policy.MaxAttempts.Should().Be(8);
    }

    [Fact]
    public void JobTypes_AssignedOrdinalDictionary_MatchesRegardlessOfCase()
    {
        // Arrange — overrides assigned in code must match the same way bound ones do, whatever comparer the caller's
        // dictionary was created with.
        var options = new BackgroundJobsOptions
        {
            JobTypes = new Dictionary<string, BackgroundJobTypeOptions>(StringComparer.Ordinal)
            {
                ["WebHookDelivery"] = new() { MaxAttempts = 8 },
            },
        };

        // Act
        var policy = options.ResolvePolicy("webhookdelivery");

        // Assert
        policy.MaxAttempts.Should().Be(8);
    }

    [Fact]
    public void JobTypes_AssignedKeysDifferOnlyInCase_Throws()
    {
        // Arrange
        var options = new BackgroundJobsOptions();
        var jobTypes = new Dictionary<string, BackgroundJobTypeOptions>(StringComparer.Ordinal)
        {
            ["EXPORT"] = new() { MaxAttempts = 5 },
            ["export"] = new() { MaxAttempts = 8 },
        };

        // Act
        var act = () => options.JobTypes = jobTypes;

        // Assert — once keys match regardless of case both entries name the same job type, and neither may silently
        // win.
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void JobTypes_AssignedNull_Throws()
    {
        // Arrange
        var options = new BackgroundJobsOptions();

        // Act
        var act = () => options.JobTypes = null!;

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public void Validate_InvalidValue_FailsNamingKey(
        string caseId,
        Dictionary<string, string?> section,
        string[] expectedKeys)
    {
        // Arrange
        var options = Bind(section);
        var validator = new BackgroundJobsOptionsValidator();

        // Act
        var result = validator.Validate(Options.DefaultName, options);

        // Assert — an operator finds the value by its full configuration key.
        result.Failed.Should().BeTrue("the case '{0}' is invalid", caseId);
        result.FailureMessage.Should().ContainAll(expectedKeys.Select(FullKey));
    }

    [Fact]
    public void Validate_UnknownSections_Succeeds()
    {
        // Arrange — overrides for a job type whose handler is not deployed yet are not mistakes.
        var options = Bind(new() { ["JobTypes:UnknownType:MaxAttempts"] = "5" });
        var validator = new BackgroundJobsOptionsValidator();

        // Act
        var result = validator.Validate(Options.DefaultName, options);

        // Assert
        result.Succeeded.Should().BeTrue("validation reported: {0}", result.FailureMessage);
    }

    [Theory]
    [InlineData("SweepIntervalSeconds")]
    [InlineData("MaxConcurrency")]
    [InlineData("HeartbeatIntervalSeconds")]
    [InlineData("BacklogWarningMinutes")]
    public void Bind_RemovedKey_BindsToNothingAndStartupSucceeds(string removedKey)
    {
        // Arrange — keys the scheduler made obsolete may linger in deployed configuration and must stay harmless.
        using var provider = ModuleProvider(new() { [removedKey] = "4" });

        // Act
        var validate = () => provider.GetRequiredService<IStartupValidator>().Validate();

        // Assert
        validate.Should().NotThrow();
        typeof(BackgroundJobsOptions).GetProperty(removedKey).Should().BeNull();
    }

    [Fact]
    public void Validate_NegativeJobTypeMaxConcurrency_FailsStartupNamingMaxConcurrency()
    {
        // Arrange
        using var provider = ModuleProvider(new() { ["JobTypes:X:MaxConcurrency"] = "-1" });

        // Act
        var thrown = Record.Exception(() => provider.GetRequiredService<IStartupValidator>().Validate());

        // Assert — the scheduler's pool size reads the same options, so startup can report the failure more than
        // once; every report is the same validation failure.
        thrown.Should().NotBeNull();
        var failures = thrown is AggregateException aggregate ? aggregate.Flatten().InnerExceptions : [thrown];
        failures.Should().NotBeEmpty().And.AllBeOfType<OptionsValidationException>();
        failures.Should().OnlyContain(failure => failure.Message.Contains("MaxConcurrency"));
    }

    [Fact]
    public void ResolvePolicy_NoMaxConcurrencyOverride_DefaultsToOne()
    {
        // Arrange
        var options = Bind(new() { ["JobTypes:WebHookDelivery:MaxConcurrency"] = "4" });

        // Act
        var overridden = options.ResolvePolicy("WebHookDelivery");
        var unset = options.ResolvePolicy("SubmissionExport");

        // Assert
        overridden.MaxConcurrency.Should().Be(4);
        unset.MaxConcurrency.Should().Be(1);
    }

    [Fact]
    public void Validate_ZeroJobTypeMaxConcurrency_Succeeds()
    {
        // Arrange — zero keeps a node from running a job type it still enqueues.
        var options = Bind(new() { ["JobTypes:X:MaxConcurrency"] = "0" });
        var validator = new BackgroundJobsOptionsValidator();

        // Act
        var result = validator.Validate(Options.DefaultName, options);

        // Assert
        result.Succeeded.Should().BeTrue("validation reported: {0}", result.FailureMessage);
    }

    // The module's own registration, as a host runs it, so the test sees what startup validation sees.
    private static ServiceProvider ModuleProvider(Dictionary<string, string?> section)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(section.Select(entry => KeyValuePair.Create(FullKey(entry.Key), entry.Value)))
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=endatix;Username=endatix;Password=endatix",
                ["ConnectionStrings:DefaultConnection_DbProvider"] = "postgresql",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        JobsModule.Instance.ConfigureServices(new EndatixModuleBuilder(services, configuration));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Binds <paramref name="section"/>, whose keys are relative to <see cref="BackgroundJobsOptions.SectionName"/>,
    /// over a default instance.
    /// </summary>
    private static BackgroundJobsOptions Bind(Dictionary<string, string?> section)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(section.Select(entry => KeyValuePair.Create(FullKey(entry.Key), entry.Value)))
            .Build();

        var options = new BackgroundJobsOptions();
        configuration.GetSection(BackgroundJobsOptions.SectionName).Bind(options);
        return options;
    }

    private static string FullKey(string key) => $"{BackgroundJobsOptions.SectionName}:{key}";
}
