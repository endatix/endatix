using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class BackgroundJobHandlerRegistryTests
{
    /// <summary>
    /// Each case is the job types of the handlers registered together, with text the exception has to name so that
    /// whoever reads it knows which handler to fix. The long one is one character past the job type column.
    /// </summary>
    public static TheoryData<string[], string> UnroutableJobTypes => new()
    {
        { ["Dup", "Dup"], "Dup" },
        { [""], nameof(TestJobHandler) },
        { ["   "], nameof(TestJobHandler) },
        { [new string('a', 129)], "128" },
    };

    [Theory]
    [MemberData(nameof(UnroutableJobTypes))]
    public void Validate_DuplicateBlankOrLongJobType_Throws(string[] jobTypes, string named)
    {
        // Arrange
        var registry = CreateRegistry(jobTypes);

        // Act
        Action validating = registry.Validate;

        // Assert
        validating.Should().Throw<InvalidOperationException>().WithMessage($"*{named}*");
    }

    [Fact]
    public void Validate_NoHandlers_Succeeds()
    {
        // Arrange
        var registry = CreateRegistry([]);

        // Act
        Action validating = registry.Validate;

        // Assert
        validating.Should().NotThrow();
        registry.JobTypes.Should().BeEmpty();
    }

    private static BackgroundJobHandlerRegistry CreateRegistry(string[] jobTypes)
    {
        var services = new ServiceCollection();
        foreach (var jobType in jobTypes)
        {
            services.AddScoped<IBackgroundJobHandler>(_ => new TestJobHandler(jobType));
        }

        return new BackgroundJobHandlerRegistry(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    private sealed class TestJobHandler(string jobType) : IBackgroundJobHandler
    {
        public string JobType => jobType;

        public Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Validation resolves handlers; it never runs them.");
    }
}
