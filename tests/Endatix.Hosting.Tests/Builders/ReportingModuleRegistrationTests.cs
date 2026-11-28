using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.UseCases.FormDrafts;
using Endatix.Framework.Modules;
using Endatix.Hosting.Builders;
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
    private const string POSTGRESQL_CONNECTION_STRING = "Host=localhost;Database=endatix;Username=endatix;Password=endatix";
    private const string SQLSERVER_CONNECTION_STRING = "Server=localhost;Database=endatix;User Id=sa;Password=endatix;TrustServerCertificate=True";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseDefaults_WithOrWithoutReporting_ChecksGeneratedKeysPastTheTenantLimitOnce(bool enabled)
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateModuleBuilder(services, "postgresql").Configuration;
        if (enabled)
        {
            config["Endatix:FeatureFlags:ReportingModule"] = "true";
        }
        config["Endatix:Auth:Providers:EndatixJwt:Enabled"] = "true";
        config["Endatix:Auth:Providers:EndatixJwt:SigningKey"] = Guid.NewGuid().ToString("N");
        config["Customizations:Reporting:MaxQuestionsByTenant:42"] = "1";
        services.AddSingleton(config);
        var builder = new EndatixBuilder(services, config);
        builder.UseDefaults().FinalizeConfiguration();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var check = scope.ServiceProvider.GetServices<IFormDefinitionCheck>().Single(c => c.Code == "column/duplicate-key");
        var definition = JsonSerializer.Deserialize<JsonElement>("""
            {"elements":[{"type":"text","name":"a"},{"type":"text","name":"a"},{"type":"matrix","name":"b","rows":["r","r"]}]}
            """);

        // Act
        var findings = await check.RunAsync(new(42, 1, 0, "", definition), TestContext.Current.CancellationToken);

        // Assert
        services.Any(d => d.ServiceType == typeof(IReportingDbContext)).Should().Be(enabled);
        findings.Count(f => f.Code == "column/duplicate-key" && f.Blocking).Should().Be(2);
        var limit = findings.Single(f => f.Code == "reporting/compile-limit");
        limit.Blocking.Should().BeFalse();
        limit.RequiresAck.Should().BeTrue();
        limit.Data!.Value.GetProperty("actual").GetInt32().Should().Be(3);
        limit.Data.Value.GetProperty("limit").GetInt32().Should().Be(1);
    }

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
                    provider == "sqlserver" ? SQLSERVER_CONNECTION_STRING : POSTGRESQL_CONNECTION_STRING,
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider,
            })
            .Build();
        return new EndatixModuleBuilder(services, configuration);
    }
}
