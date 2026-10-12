using Endatix.Core.Abstractions;
using Endatix.Framework.Modules;
using Endatix.Modules.Reporting;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Hosting.Tests.Builders;

/// <summary>
/// The Reporting module registers the context of the configured database provider, and only that one.
/// </summary>
public class ReportingModuleRegistrationTests
{
    private const string PostgreSqlConnectionString = "Host=localhost;Database=endatix;Username=endatix;Password=endatix";
    private const string SqlServerConnectionString = "Server=localhost;Database=endatix;User Id=sa;Password=endatix;TrustServerCertificate=True";

    [Fact]
    public void ConfigureServices_PostgreSql_RegistersAMigrationContributor()
    {
        // Arrange
        var moduleBuilder = CreateModuleBuilder(new ServiceCollection(), provider: "postgresql");

        // Act
        ReportingModule.Instance.ConfigureServices(moduleBuilder);

        // Assert
        moduleBuilder.MigrationContributorRegistered.Should().BeTrue();
    }

    [Fact]
    public void ConfigureServices_PostgreSql_RegistersOnlyThePostgreSqlContext()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ITenantContext>());
        var moduleBuilder = CreateModuleBuilder(services, provider: "postgresql");

        // Act
        ReportingModule.Instance.ConfigureServices(moduleBuilder);

        // Assert
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var asInterface = scope.ServiceProvider.GetRequiredService<IReportingDbContext>();
        var asBase = scope.ServiceProvider.GetRequiredService<ReportingDbContextBase>();
        asInterface.Should().BeOfType<ReportingPostgreSqlDbContext>();
        asBase.Should().BeSameAs(asInterface);
        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(ReportingSqlServerDbContext));
    }

    [Fact]
    public void ConfigureServices_SqlServer_RegistersOnlyTheSqlServerContext()
    {
        // Arrange — reaching ConfigureServices means the flag is on, so the host asked for Reporting.
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ITenantContext>());
        var moduleBuilder = CreateModuleBuilder(services, provider: "sqlserver");

        // Act
        ReportingModule.Instance.ConfigureServices(moduleBuilder);

        // Assert
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var asInterface = scope.ServiceProvider.GetRequiredService<IReportingDbContext>();
        var asBase = scope.ServiceProvider.GetRequiredService<ReportingDbContextBase>();
        asInterface.Should().BeOfType<ReportingSqlServerDbContext>();
        asBase.Should().BeSameAs(asInterface);
        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(ReportingPostgreSqlDbContext));
    }

    private static EndatixModuleBuilder CreateModuleBuilder(IServiceCollection services, string provider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    provider == "sqlserver" ? SqlServerConnectionString : PostgreSqlConnectionString,
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider,
            })
            .Build();
        return new EndatixModuleBuilder(services, configuration);
    }
}
