using Endatix.Core.Abstractions;
using Endatix.Framework.Modules;
using Endatix.Modules.Audience;
using Endatix.Modules.Audience.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Hosting.Tests.Builders;

/// <summary>
/// The Audience module registers the context of the configured provider and only that one.
/// </summary>
public class AudienceModuleRegistrationTests
{
    [Fact]
    public void ConfigureServices_SqlServer_RegistersTheSqlServerAudienceContext()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ITenantContext>());
        var moduleBuilder = CreateModuleBuilder(
            services,
            provider: "sqlserver",
            connectionString: "Server=localhost;Database=endatix;User Id=sa;Password=not-connected");

        // Act
        AudienceModule.Instance.ConfigureServices(moduleBuilder);

        // Assert
        AssertRegisteredContext<AudienceSqlServerDbContext, AudiencePostgreSqlDbContext>(services, moduleBuilder);
    }

    [Fact]
    public void ConfigureServices_PostgreSql_RegistersThePostgreSqlAudienceContext()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ITenantContext>());
        var moduleBuilder = CreateModuleBuilder(
            services,
            provider: "postgresql",
            connectionString: "Host=localhost;Database=endatix;Username=endatix;Password=endatix");

        // Act
        AudienceModule.Instance.ConfigureServices(moduleBuilder);

        // Assert
        AssertRegisteredContext<AudiencePostgreSqlDbContext, AudienceSqlServerDbContext>(services, moduleBuilder);
    }

    private static void AssertRegisteredContext<TRegistered, TOther>(
        ServiceCollection services,
        EndatixModuleBuilder moduleBuilder)
    {
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var asInterface = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var asBase = scope.ServiceProvider.GetRequiredService<AudienceDbContextBase>();
        asInterface.Should().BeOfType<TRegistered>();
        asBase.Should().BeSameAs(asInterface);
        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(TOther));
        moduleBuilder.MigrationContributorRegistered.Should().BeTrue();
    }

    private static EndatixModuleBuilder CreateModuleBuilder(
        IServiceCollection services,
        string provider,
        string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider,
            })
            .Build();
        return new EndatixModuleBuilder(services, configuration);
    }
}
