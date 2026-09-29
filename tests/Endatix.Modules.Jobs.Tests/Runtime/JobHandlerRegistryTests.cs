using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    [Fact]
    public void Resolve_KeyedHandlers_BuildsOnlyTheResolvedJobType()
    {
        // Arrange
        var built = new List<string>();
        var services = new ServiceCollection();
        services.AddBackgroundJobHandler("A", _ => Built(built, "A"));
        services.AddBackgroundJobHandler("B", _ => Built(built, "B"));
        using var provider = services.BuildServiceProvider();
        var registry = JobHandlerRegistry.Build(provider);
        built.Clear();
        using var scope = provider.CreateScope();

        // Act
        var handler = registry.Resolve(scope.ServiceProvider, "A");

        // Assert
        handler!.JobType.Should().Be("A");
        built.Should().Equal("A");
    }

    [Fact]
    public void Resolve_TypedHandlerRegisteredByPayload_ReturnsThatHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackgroundJobHandler<TypedHandler, TypedPayload>();
        services.AddBackgroundJobHandler("Other", _ => new NamedHandler("Other"));
        using var provider = services.BuildServiceProvider();
        var registry = JobHandlerRegistry.Build(provider);
        using var scope = provider.CreateScope();

        // Act
        var handler = registry.Resolve(scope.ServiceProvider, TypedPayload.JobType);

        // Assert
        handler.Should().BeOfType<TypedHandler>();
    }

    [Fact]
    public void Resolve_SameHandlerTypeForTwoJobTypesWithoutKeys_ReturnsTheOneDeclaringTheJobType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBackgroundJobHandler>(_ => new NamedHandler("A"));
        services.AddScoped<IBackgroundJobHandler>(_ => new NamedHandler("B"));
        using var provider = services.BuildServiceProvider();
        var registry = JobHandlerRegistry.Build(provider);
        using var scope = provider.CreateScope();

        // Act
        var handler = registry.Resolve(scope.ServiceProvider, "B");

        // Assert
        handler!.JobType.Should().Be("B");
    }

    [Fact]
    public void Build_KeyedHandlerDeclaringAnotherJobType_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBackgroundJobHandler("A", _ => new NamedHandler("B"));
        services.AddScoped<IBackgroundJobHandler>(_ => new NamedHandler("A"));
        using var provider = services.BuildServiceProvider();

        // Act
        var build = () => JobHandlerRegistry.Build(provider);

        // Assert
        build.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("'A'").And.Contain("'B'");
    }

    private static NamedHandler Built(List<string> built, string jobType)
    {
        built.Add(jobType);
        return new NamedHandler(jobType);
    }

    private sealed record TypedPayload(long Id) : IBackgroundJobPayload
    {
        public static string JobType => "Typed";
    }

    private sealed class TypedHandler(ILogger<TypedHandler> logger) : BackgroundJobHandler<TypedPayload>(logger)
    {
        protected override Task<Result> ExecuteAsync(
            BackgroundJobContext job, TypedPayload payload, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
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
