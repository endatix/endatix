using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.Configuration;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class QuartzRegistrationTests
{
    private static readonly string[] ReportingJobTypes =
    [
        "ReportingCompileFormSchema",
        "ReportingFlattenSubmission",
        "ReportingSeedDefaultExportFormats",
        "ReportingSyncFormDeletion",
        "ReportingSyncSubmissionDeletion",
    ];

    [Fact]
    public void Build_RegisteredJobTypes_PoolSizeIsSumOfCaps()
    {
        // Arrange
        var options = Bind(new Dictionary<string, string?>
        {
            ["JobTypes:WebHookDelivery:MaxConcurrency"] = "4",
            ["JobTypes:ReportingCompileFormSchema:MaxConcurrency"] = "2",
            ["JobTypes:ReportingFlattenSubmission:MaxConcurrency"] = "2",
            ["JobTypes:ReportingSeedDefaultExportFormats:MaxConcurrency"] = "2",
            ["JobTypes:ReportingSyncFormDeletion:MaxConcurrency"] = "2",
            ["JobTypes:ReportingSyncSubmissionDeletion:MaxConcurrency"] = "2",
        });
        string[] jobTypes = ["WebHookDelivery", .. ReportingJobTypes];

        // Act
        var plan = QuartzRegistration.Build(jobTypes, options);

        // Assert
        plan.PoolSize.Should().Be(14);
    }

    [Fact]
    public void Build_GlobalMaxConcurrencyConfigured_BindsToNothing()
    {
        // Arrange — the pool used to be one global number; that key no longer sizes anything.
        var options = Bind(new Dictionary<string, string?> { ["MaxConcurrency"] = "4" });

        // Act
        var plan = QuartzRegistration.Build(["A", "B"], options);

        // Assert — two job types at the default cap of one each.
        plan.PoolSize.Should().Be(2);
    }

    [Fact]
    public void Build_EveryJobTypeCappedAtZero_KeepsOneThread()
    {
        // Arrange
        var options = Bind(new Dictionary<string, string?> { ["JobTypes:A:MaxConcurrency"] = "0" });

        // Act
        var plan = QuartzRegistration.Build(["A"], options);

        // Assert
        plan.PoolSize.Should().Be(1);
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
