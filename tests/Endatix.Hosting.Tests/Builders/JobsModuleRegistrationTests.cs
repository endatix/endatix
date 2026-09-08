using System.Collections;
using System.Reflection;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Framework.Modules;
using Endatix.Hosting.Builders;
using Endatix.Modules.Jobs;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Hosting.Tests.Builders;

/// <summary>
/// The Jobs module owns a schema and migrations but currently supports one provider and has no
/// consumer, so what a host does with it is decided entirely by the feature flag and the configured
/// provider. These are the combinations a host can actually be in.
/// </summary>
public class JobsModuleRegistrationTests
{
    private const string PostgreSqlConnectionString =
        "Host=localhost;Database=endatix;Username=endatix;Password=endatix";

    [Fact]
    public void UseModule_FlagAbsent_DoesNotRegisterTheModule()
    {
        // Arrange — a host that has never heard of the flag, which is every host today.
        var builder = CreateBuilder(flagEnabled: null, isPostgreSql: true);

        // Act
        builder.UseModule(JobsModule.Instance);

        // Assert
        GetRegisteredModuleCount(builder).Should().Be(0);
    }

    [Fact]
    public void UseModule_FlagDisabled_DoesNotRegisterTheModule()
    {
        // Arrange
        var builder = CreateBuilder(flagEnabled: false, isPostgreSql: true);

        // Act
        builder.UseModule(JobsModule.Instance);

        // Assert
        GetRegisteredModuleCount(builder).Should().Be(0);
    }

    [Fact]
    public void UseModule_FlagEnabled_RegistersTheModule()
    {
        // Arrange
        var builder = CreateBuilder(flagEnabled: true, isPostgreSql: true);

        // Act
        builder.UseModule(JobsModule.Instance);

        // Assert
        GetRegisteredModuleCount(builder).Should().Be(1);
    }

    [Fact]
    public void ConfigureServices_PostgreSql_RegistersTheQueueOverAJobsContext()
    {
        // Arrange
        var services = new ServiceCollection();
        var moduleBuilder = CreateModuleBuilder(services, isPostgreSql: true);

        // Act
        JobsModule.Instance.ConfigureServices(moduleBuilder);

        // Assert
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IBackgroundJobQueue));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IJobsDbContext));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(JobsPostgreSqlDbContext));
    }

    [Fact]
    public void ConfigureServices_PostgreSql_RegistersAMigrationContributor()
    {
        // Arrange
        var moduleBuilder = CreateModuleBuilder(new ServiceCollection(), isPostgreSql: true);

        // Act
        JobsModule.Instance.ConfigureServices(moduleBuilder);

        // Assert — the module declares IHasDbMigrations, and a declared-but-absent contributor makes
        // the host log a wiring fault at every startup.
        moduleBuilder.MigrationContributorRegistered.Should().BeTrue();
    }

    [Fact]
    public void ConfigureServices_ProviderIsNotPostgreSql_FailsNamingPostgreSql()
    {
        // Arrange — reaching ConfigureServices means the flag is on, so the host asked for jobs.
        var services = new ServiceCollection();
        var moduleBuilder = CreateModuleBuilder(services, isPostgreSql: false);

        // Act
        var act = () => JobsModule.Instance.ConfigureServices(moduleBuilder);

        // Assert — failing here beats registering something that throws at the first enqueue,
        // which on the webhook path would be an outbox tick rather than a startup log.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PostgreSQL*");
        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(IBackgroundJobQueue));
        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(JobsPostgreSqlDbContext));
    }

    private static IConfiguration CreateConfiguration(bool? flagEnabled, bool isPostgreSql)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = PostgreSqlConnectionString,
            ["ConnectionStrings:DefaultConnection_DbProvider"] = isPostgreSql ? "postgresql" : "sqlserver",
        };

        if (flagEnabled is not null)
        {
            settings[$"Endatix:FeatureFlags:{Endatix.Framework.FeatureFlags.FeatureFlags.JobsModule}"] = flagEnabled.Value.ToString();
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private static EndatixBuilder CreateBuilder(bool? flagEnabled, bool isPostgreSql) =>
        new(new ServiceCollection(), CreateConfiguration(flagEnabled, isPostgreSql));

    private static EndatixModuleBuilder CreateModuleBuilder(IServiceCollection services, bool isPostgreSql) =>
        new(services, CreateConfiguration(flagEnabled: true, isPostgreSql));

    private static int GetRegisteredModuleCount(EndatixBuilder builder)
    {
        var modulesField = typeof(EndatixBuilder).GetField(
            "_modules", BindingFlags.Instance | BindingFlags.NonPublic);
        return ((IList)modulesField!.GetValue(builder)!).Count;
    }
}
