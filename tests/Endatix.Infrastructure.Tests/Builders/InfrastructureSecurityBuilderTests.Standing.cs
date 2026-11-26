using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Infrastructure.Builders;
using Endatix.Infrastructure.Features.AccessControl;
using Endatix.Infrastructure.Identity;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Builders;

public partial class InfrastructureSecurityBuilderTests
{
    [Fact]
    public void AddRequiredActionEnforcement_BeforeSecurityDefaults_EnforcesOncePerScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped(_ => Substitute.For<ICurrentUserAuthorizationService>());

        // Act
        services.AddRequiredActionEnforcement();
        services.AddEndatixSecurityServices(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        // Assert
        var standing = Assert.Single(first.ServiceProvider.GetServices<AccountStanding>());
        Assert.True(standing.IsEnforced);
        Assert.Same(standing, first.ServiceProvider.GetRequiredService<AccountStanding>());
        Assert.NotSame(standing, second.ServiceProvider.GetRequiredService<AccountStanding>());
    }

    [Fact]
    public void UseDefaults_WithConfiguredStanding_PreservesTheCallerRegistration()
    {
        // Arrange
        var standing = new AccountStanding(Substitute.For<ICurrentUserAuthorizationService>());
        _services.AddScoped(_ => standing);

        // Act
        new InfrastructureSecurityBuilder(_parentBuilder).UseDefaults();
        using var provider = _services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Assert
        Assert.Same(standing, Assert.Single(scope.ServiceProvider.GetServices<AccountStanding>()));
    }

    [Fact]
    public void AddEndatixSecurityServices_Standalone_ResolvesFormAccessWithDormantStanding()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ICurrentUserAuthorizationService>());
        services.AddSingleton(Substitute.For<IDateTimeProvider>());
        services.AddSingleton(Substitute.For<HybridCache>());

        // Act
        services.AddEndatixSecurityServices(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<IResourceAccessQuery<FormAccessData, FormAccessContext>>();

        // Assert
        Assert.IsType<FormAccessPolicy>(access);
        Assert.False(scope.ServiceProvider.GetRequiredService<AccountStanding>().IsEnforced);
    }
}
