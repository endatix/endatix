using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class RecurringSchedulesTests
{
    [Fact]
    public void AddRecurringJob_IdenticalContributions_DeclareOneScheduleAndHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        const string name = "Sweep";

        // Act
        services.AddRecurringJob<Probe>(name, "0 0 2 * * ?");
        services.AddRecurringJob<Probe>(name, "0 0 2 * * ?");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Assert
        provider.GetServices<RecurringJobSchedule>().Should().ContainSingle();
        scope.ServiceProvider.GetKeyedServices<IRecurringJob>(name).Should().ContainSingle();
    }

    [Theory]
    [InlineData("sweep", "0 0 2 * * ?")]
    [InlineData("Sweep", "0 0 3 * * ?")]
    public void Build_AmbiguousContributions_RefusesTheSchedule(string name, string cron)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRecurringJob<Probe>("Sweep", "0 0 2 * * ?");
        services.AddRecurringJob<Probe>(name, cron);
        using var provider = services.BuildServiceProvider();

        // Act
        var build = () => new RecurringSchedules(provider.GetServices<RecurringJobSchedule>(), Options.Create(new BackgroundJobsOptions()));

        // Assert
        build.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate recurring schedule*");
    }

    private sealed class Probe : IRecurringJob
    {
        public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
