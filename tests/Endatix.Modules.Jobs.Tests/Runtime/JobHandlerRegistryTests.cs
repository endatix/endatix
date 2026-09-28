using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class JobHandlerRegistryTests
{
    [Fact]
    public void Build_DuplicateJobType_Throws()
    {
        // Arrange
        IBackgroundJobHandler[] handlers = [new FirstDupHandler(), new SecondDupHandler()];

        // Act
        var build = () => JobHandlerRegistry.Build(handlers);

        // Assert
        build.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Dup");
    }

    [Fact]
    public void Build_UnknownJobType_IsNotAnError()
    {
        // Arrange — "Orphan" has no handler on this host; another host may run it.
        IBackgroundJobHandler[] handlers = [new FirstDupHandler()];

        // Act
        var registry = JobHandlerRegistry.Build(handlers);

        // Assert
        registry.Contains("Orphan").Should().BeFalse();
        registry.JobTypes.Should().Equal("Dup");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_BlankJobType_Throws(string jobType)
    {
        // Arrange
        IBackgroundJobHandler[] handlers = [new NamedHandler(jobType)];

        // Act
        var build = () => JobHandlerRegistry.Build(handlers);

        // Assert
        build.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Build_JobTypeLongerThanColumn_Throws()
    {
        // Arrange
        IBackgroundJobHandler[] handlers = [new NamedHandler(new string('x', 129))];

        // Act
        var build = () => JobHandlerRegistry.Build(handlers);

        // Assert
        build.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Resolve_RegisteredJobType_ReturnsHandlerFromJobScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBackgroundJobHandler, FirstDupHandler>();
        services.AddScoped<IBackgroundJobHandler>(_ => new NamedHandler("Other"));
        using var provider = services.BuildServiceProvider();
        var registry = JobHandlerRegistry.Build(provider);
        using var scope = provider.CreateScope();

        // Act
        var handler = registry.Resolve(scope.ServiceProvider, "Dup");
        var missing = registry.Resolve(scope.ServiceProvider, "Orphan");

        // Assert
        handler.Should().BeOfType<FirstDupHandler>();
        missing.Should().BeNull();
    }

    private sealed class FirstDupHandler : IBackgroundJobHandler
    {
        public string JobType => "Dup";

        public Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }

    private sealed class SecondDupHandler : IBackgroundJobHandler
    {
        public string JobType => "Dup";

        public Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }

    private sealed class NamedHandler(string jobType) : IBackgroundJobHandler
    {
        public string JobType => jobType;

        public Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }
}
