using Endatix.Core.Infrastructure.Messaging;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Builders;

public class InfrastructureMessagingBuilderTests
{
    private readonly InfrastructureBuilder _parentBuilder;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly ServiceCollection _services;
    private readonly IBuilderRoot _builderRoot;

    public InfrastructureMessagingBuilderTests()
    {
        _services = new ServiceCollection();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _logger = Substitute.For<ILogger>();

        // Create a substitute for IBuilderRoot (matches current builder setup)
        _builderRoot = Substitute.For<IBuilderRoot>();
        _builderRoot.Services.Returns(_services);
        _builderRoot.LoggerFactory.Returns(_loggerFactory);
        _builderRoot.Configuration.Returns(Substitute.For<IConfiguration>());
        _builderRoot.AppEnvironment.Returns((IAppEnvironment?)null);

        // Create a real InfrastructureBuilder with the mocked builder root
        _parentBuilder = new InfrastructureBuilder(_builderRoot);

        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(_logger);
    }

    // Helper method to find MediatR registration
    private ServiceDescriptor? FindMediatRDescriptor()
    {
        return _services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(MediatR.IMediator));
    }

    // Helper method to find logging pipeline behavior registration
    private ServiceDescriptor? FindLoggingPipelineBehaviorDescriptor()
    {
        return _services.FirstOrDefault(sd =>
            sd.ServiceType.IsGenericType &&
            sd.ServiceType.GetGenericTypeDefinition() == typeof(MediatR.IPipelineBehavior<,>) &&
            sd.ImplementationType?.Name.Contains("LoggingPipelineBehavior") == true);
    }

    // Helper method to find domain event dispatcher registration
    private ServiceDescriptor? FindDomainEventDispatcherDescriptor()
    {
        return _services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(IDomainEventDispatcher));
    }

    [Fact]
    public void UseDefaults_ShouldConfigureWithDefaultSettings()
    {
        // Arrange
        var builder = new InfrastructureMessagingBuilder(_parentBuilder);

        // Act
        var result = builder.UseDefaults();
        result.Build(); // Apply the configuration

        // Assert
        Assert.Same(builder, result);
        Assert.NotNull(FindMediatRDescriptor());
        Assert.NotNull(FindLoggingPipelineBehaviorDescriptor());

        // Verify domain event dispatcher is registered
        var eventDispatcherDescriptor = FindDomainEventDispatcherDescriptor();
        Assert.NotNull(eventDispatcherDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, eventDispatcherDescriptor.Lifetime);
        Assert.Equal(typeof(MediatRDomainEventDispatcher), eventDispatcherDescriptor.ImplementationType);
    }

    [Fact]
    public void Configure_ShouldStoreAndApplyConfigurationAction()
    {
        // Arrange
        var builder = new InfrastructureMessagingBuilder(_parentBuilder);

        // Act
        var result = builder.Configure(options =>
        {
            options.IncludeLoggingPipeline = false; // Turn off logging pipeline
        });
        result.Build(); // Apply the configuration

        // Assert
        Assert.Same(builder, result);
        Assert.NotNull(FindMediatRDescriptor());
        Assert.Null(FindLoggingPipelineBehaviorDescriptor());
        Assert.NotNull(FindDomainEventDispatcherDescriptor());
    }

    [Fact]
    public void Configure_ShouldThrowException_WhenConfigureActionIsNull()
    {
        // Arrange
        var builder = new InfrastructureMessagingBuilder(_parentBuilder);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.Configure(null!));
    }

    [Fact]
    public void Configure_ShouldComposeMultipleConfigurations()
    {
        // Arrange
        var builder = new InfrastructureMessagingBuilder(_parentBuilder);
        var firstConfigExecuted = false;
        var secondConfigExecuted = false;

        // Act - Apply two configurations
        builder.Configure(options =>
        {
            options.IncludeLoggingPipeline = true;
            firstConfigExecuted = true;
        });

        builder.Configure(options =>
        {
            options.AdditionalAssemblies = [typeof(InfrastructureMessagingBuilderTests).Assembly];
            secondConfigExecuted = true;
        });

        // Build to apply the configuration
        builder.Build();

        // Assert
        Assert.NotNull(FindMediatRDescriptor());
        Assert.True(firstConfigExecuted);
        Assert.True(secondConfigExecuted);
        Assert.NotNull(FindLoggingPipelineBehaviorDescriptor());
        Assert.NotNull(FindDomainEventDispatcherDescriptor());
    }

    [Fact]
    public void Configure_ShouldRegisterAdditionalAssemblies()
    {
        // Arrange
        var builder = new InfrastructureMessagingBuilder(_parentBuilder);
        var testAssembly = typeof(InfrastructureMessagingBuilderTests).Assembly;

        // Act
        builder.Configure(options =>
        {
            options.AdditionalAssemblies = [testAssembly];
        });
        builder.Build();

        // Assert
        var mediatrDescriptor = FindMediatRDescriptor();
        Assert.NotNull(mediatrDescriptor);
        Assert.NotNull(FindDomainEventDispatcherDescriptor());
    }

    [Fact]
    public void Build_ShouldReturnParentBuilder()
    {
        // Arrange
        var builder = new InfrastructureMessagingBuilder(_parentBuilder);

        // Act
        var result = builder.Build();

        // Assert
        Assert.Same(_parentBuilder, result);
    }

    [Fact]
    public void Build_ShouldApplyConfiguration_OnlyOnce()
    {
        // Arrange
        var builder = new InfrastructureMessagingBuilder(_parentBuilder);
        var initialServiceCount = _services.Count;

        // Act - Build twice
        builder.UseDefaults();
        var result1 = builder.Build();
        var countAfterFirstBuild = _services.Count;
        var result2 = builder.Build();
        var countAfterSecondBuild = _services.Count;

        // Assert
        Assert.Same(_parentBuilder, result1);
        Assert.Same(_parentBuilder, result2);

        // Verify services were only registered once
        Assert.True(countAfterFirstBuild > initialServiceCount);
        Assert.Equal(countAfterFirstBuild, countAfterSecondBuild);

        Assert.NotNull(FindMediatRDescriptor());
        Assert.NotNull(FindDomainEventDispatcherDescriptor());
    }

    [Fact]
    public void Build_ShouldApplyDefaultConfiguration_WhenNoConfigurationWasProvided()
    {
        // Arrange
        var builder = new InfrastructureMessagingBuilder(_parentBuilder);

        // Act - Don't call UseDefaults() or Configure()
        var result = builder.Build();

        // Assert
        Assert.Same(_parentBuilder, result);

        Assert.NotNull(FindMediatRDescriptor());
        Assert.NotNull(FindDomainEventDispatcherDescriptor());
        Assert.NotNull(FindLoggingPipelineBehaviorDescriptor());
    }
}