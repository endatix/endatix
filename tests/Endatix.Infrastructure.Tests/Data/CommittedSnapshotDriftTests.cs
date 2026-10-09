using System.Reflection;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Identity;
using Endatix.Modules.Audience.Persistence;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Reporting.Persistence;
using Endatix.Persistence.PostgreSql.Builders;
using Endatix.Persistence.SqlServer.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Data;

/// <summary>
/// Every committed model snapshot must be exactly what <c>migrations add</c> would write for the model its context
/// builds today. <c>HasPendingModelChanges</c> catches a relational difference, such as a lost provider annotation;
/// the scaffolded snapshot also catches stale text that maps to the same relational model, such as entity order.
/// The contexts are registered the way the hosts register them, so each one reads the snapshot the runtime reads;
/// no connection is opened. <see cref="CommittedModelProviderAnnotationsTests"/> covers migration designers.
/// </summary>
public sealed class CommittedSnapshotDriftTests
{
    private const string PostgreSql = "postgresql";
    private const string SqlServer = "sqlserver";

    private static readonly Dictionary<string, string> ConnectionStrings = new()
    {
        [PostgreSql] = "Host=127.0.0.1;Database=__ef_model_inspection_not_connected__;Username=postgres;Password=postgres",
        [SqlServer] = "Server=127.0.0.1;Database=__ef_model_inspection_not_connected__;User Id=sa;Password=not-connected",
    };

    public static TheoryData<string, Type> MigratedContexts => new()
    {
        { PostgreSql, typeof(AppDbContext) },
        { PostgreSql, typeof(AppIdentityDbContext) },
        { PostgreSql, typeof(ReportingPostgreSqlDbContext) },
        { PostgreSql, typeof(JobsPostgreSqlDbContext) },
        { PostgreSql, typeof(AudiencePostgreSqlDbContext) },
        { SqlServer, typeof(AppDbContext) },
        { SqlServer, typeof(AppIdentityDbContext) },
    };

    [Theory]
    [MemberData(nameof(MigratedContexts))]
    public void HasPendingModelChanges_CommittedSnapshot_ReturnsFalse(string provider, Type contextType)
    {
        // Arrange
        using var services = ServicesFor(provider);
        var context = (DbContext)services.GetRequiredService(contextType);

        // Act
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot;
        var hasPendingModelChanges = context.Database.HasPendingModelChanges();

        // Assert
        snapshot.Should().NotBeNull("the context must find its committed snapshot, or there is nothing to compare");
        hasPendingModelChanges.Should().BeFalse(
            "the committed snapshot must match the current model; scaffold a migration to see the difference");
    }

    [Theory]
    [MemberData(nameof(MigratedContexts))]
    public void ScaffoldMigration_CommittedSnapshot_RewritesTheSameSnapshot(string provider, Type contextType)
    {
        // Arrange
        using var services = ServicesFor(provider);
        var context = (DbContext)services.GetRequiredService(contextType);
        var snapshotType = context.GetService<IMigrationsAssembly>().ModelSnapshot!.GetType();
        var committedCode = CommittedSnapshotCode(snapshotType);

        // Act
        var scaffoldedCode = ScaffoldedSnapshotCode(context, snapshotType.Namespace).ReplaceLineEndings("\n");

        // Assert
        scaffoldedCode.Should().Be(
            committedCode,
            "migrations add must leave the committed snapshot unchanged; scaffold a probe migration to see the difference");
    }

    [Fact]
    public void MigratedContexts_EverySnapshotInTheBuild_IsChecked()
    {
        // Arrange
        var snapshotsInBuild = CommittedModels.Types.Where(type => type.IsSubclassOf(typeof(ModelSnapshot)));

        // Act
        var checkedSnapshots = MigratedContexts
            .Select(row => SnapshotTypeOf(row.Data.Item1, row.Data.Item2))
            .OfType<Type>()
            .ToList();

        // Assert
        snapshotsInBuild.Should().BeSubsetOf(
            checkedSnapshots,
            "every context with committed migrations must be listed in {0}", nameof(MigratedContexts));
    }

    private static Type? SnapshotTypeOf(string provider, Type contextType)
    {
        using var services = ServicesFor(provider);
        var context = (DbContext)services.GetRequiredService(contextType);
        return context.GetService<IMigrationsAssembly>().ModelSnapshot?.GetType();
    }

    // The same design-time services dotnet ef builds: the provider's are named by an attribute on its assembly.
    private static string ScaffoldedSnapshotCode(DbContext context, string? snapshotNamespace)
    {
        var providerAssembly = Assembly.Load(context.Database.ProviderName!);
        var providerServices = providerAssembly.GetCustomAttribute<DesignTimeProviderServicesAttribute>()!.TypeName;
        var services = new ServiceCollection()
            .AddEntityFrameworkDesignTimeServices()
            .AddDbContextDesignTimeServices(context);
        ((IDesignTimeServices)Activator.CreateInstance(providerAssembly.GetType(providerServices, throwOnError: true)!)!)
            .ConfigureDesignTimeServices(services);

        using var designTimeServices = services.BuildServiceProvider();
        return designTimeServices.GetRequiredService<IMigrationsScaffolder>()
            .ScaffoldMigration("Probe", snapshotNamespace, dryRun: true)
            .SnapshotCode;
    }

    // Both provider assemblies hold a file named AppDbContextModelSnapshot.cs, so the namespace picks one. Line
    // endings follow the checkout and the OS, not the model, so both sides compare with \n.
    private static string CommittedSnapshotCode(Type snapshotType) =>
        Directory.EnumerateFiles(CommittedModels.SourceDirectory, snapshotType.Name + ".cs", SearchOption.AllDirectories)
            .Select(path => File.ReadAllText(path).ReplaceLineEndings("\n"))
            .Single(code => code.Contains($"namespace {snapshotType.Namespace}\n", StringComparison.Ordinal));

    private static ServiceProvider ServicesFor(string provider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionStrings[provider],
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider,
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(DesignTimeDbContextDependencies.TenantContext)
            .AddSingleton<OutboxIntegrationEventDispatcher>();

        if (provider == PostgreSql)
        {
            AddPostgreSqlContexts(services, configuration);
        }
        else
        {
            new SqlServerPersistenceBuilder(services)
                .UseDefault<AppDbContext>()
                .UseDefault<AppIdentityDbContext>();
        }

        return services.BuildServiceProvider();
    }

    private static void AddPostgreSqlContexts(IServiceCollection services, IConfiguration configuration)
    {
        new PostgreSqlPersistenceBuilder(services)
            .UseDefault<AppDbContext>()
            .UseDefault<AppIdentityDbContext>();
        services.AddModuleDbContext<ReportingPostgreSqlDbContext>(configuration, ReportingPersistence.ConfigureDbContextOptions);
        services.AddModuleDbContext<JobsPostgreSqlDbContext>(configuration, JobsPersistence.ConfigureDbContextOptions);
        services.AddModuleDbContext<AudiencePostgreSqlDbContext>(configuration, AudiencePersistence.ConfigureDbContextOptions);
    }
}
