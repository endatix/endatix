using Endatix.Framework.Modules;
using Endatix.Modules.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Hosting.Tests.Builders;

/// <summary>
/// The Reporting module supports PostgreSQL only, so a host that turns it on with another provider fails at startup.
/// </summary>
public class ReportingModuleRegistrationTests
{
    private const string ConnectionString = "Host=localhost;Database=endatix;Username=endatix;Password=endatix";

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
    public void ConfigureServices_ProviderIsNotPostgreSql_FailsNamingPostgreSqlAndTheFlag()
    {
        // Arrange — reaching ConfigureServices means the flag is on, so the host asked for Reporting.
        var services = new ServiceCollection();
        var moduleBuilder = CreateModuleBuilder(services, provider: "sqlserver");

        // Act
        var act = () => ReportingModule.Instance.ConfigureServices(moduleBuilder);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("The Reporting module requires PostgreSQL.*'Endatix:FeatureFlags:ReportingModule'*");
        services.Should().BeEmpty();
    }

    private static EndatixModuleBuilder CreateModuleBuilder(IServiceCollection services, string provider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider,
            })
            .Build();
        return new EndatixModuleBuilder(services, configuration);
    }
}
