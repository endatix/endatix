using System.Globalization;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// Covers binding, per-type resolution and validation of the background job options. Options are bound from
/// configuration the way the options pipeline binds them, over a default instance, so the defaults and fallbacks
/// pinned here are exactly what an operator gets by leaving a key out — and the validator is what stops a value
/// jobs cannot run with before the runner starts.
/// </summary>
public class BackgroundJobsOptionsTests
{
    public static TheoryData<string, Dictionary<string, string?>, string[]> InvalidValues => new()
    {
        {
            "concurrency below 1",
            new() { ["MaxConcurrency"] = "0" },
            ["MaxConcurrency"]
        },
        {
            // Three five-minute heartbeats overrun a ten-minute stuck threshold. Either value can be the one to
            // fix, so the message has to name both.
            "stuck threshold shorter than three heartbeats",
            new() { ["HeartbeatIntervalSeconds"] = "300", ["StuckJobThresholdMinutes"] = "10" },
            ["HeartbeatIntervalSeconds", "StuckJobThresholdMinutes"]
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
        options.MaxConcurrency.Should().Be(4);
        options.SweepIntervalSeconds.Should().Be(10);
        options.SweepBatchSize.Should().Be(200);
        options.StuckJobThresholdMinutes.Should().Be(10);
        options.HeartbeatIntervalSeconds.Should().Be(30);
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
        // Arrange — overrides for a job type whose handler is not deployed yet, or a section another component
        // reads, are not mistakes.
        var options = Bind(new()
        {
            ["JobTypes:UnknownType:MaxAttempts"] = "5",
            ["Classes:Heavy:MaxConcurrency"] = "2",
        });
        var validator = new BackgroundJobsOptionsValidator();

        // Act
        var result = validator.Validate(Options.DefaultName, options);

        // Assert
        result.Succeeded.Should().BeTrue("validation reported: {0}", result.FailureMessage);
    }

    [Fact]
    public void Bind_BacklogWarningMinutes_DefaultsTo15AndIgnoresPerTypeKey()
    {
        // Arrange — the backlog threshold describes the sweeper rather than a kind of job, so a per-type key for it
        // is not an override. With no global key alongside it, nothing may bind over the default.
        var emptySection = new Dictionary<string, string?>();
        var perTypeKeyOnly = new Dictionary<string, string?> { ["JobTypes:X:BacklogWarningMinutes"] = "5" };
        var validator = new BackgroundJobsOptionsValidator();

        // Act
        var fromEmptySection = Bind(emptySection);
        var fromPerTypeKey = Bind(perTypeKeyOnly);
        var result = validator.Validate(Options.DefaultName, fromPerTypeKey);

        // Assert
        fromEmptySection.BacklogWarningMinutes.Should().Be(15);
        result.Succeeded.Should().BeTrue("validation reported: {0}", result.FailureMessage);
        fromPerTypeKey.BacklogWarningMinutes.Should().Be(15);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(1, true)]
    public void Validate_BacklogWarningMinutes_RequiresPositiveInteger(int minutes, bool expectedValid)
    {
        // Arrange
        var options = Bind(new() { ["BacklogWarningMinutes"] = minutes.ToString(CultureInfo.InvariantCulture) });
        var validator = new BackgroundJobsOptionsValidator();

        // Act
        var result = validator.Validate(Options.DefaultName, options);

        // Assert
        if (expectedValid)
        {
            result.Succeeded.Should().BeTrue("validation reported: {0}", result.FailureMessage);
        }
        else
        {
            result.Failed.Should().BeTrue();
            result.FailureMessage.Should().Contain(FullKey("BacklogWarningMinutes"));
        }
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
