using Endatix.Hosting.Builders;
using Endatix.Infrastructure.Data;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Endatix.Hosting.Tests.Builders;

public sealed class EndatixPersistenceBuilderStartupOrderTests
{
    [Fact]
    public void UseDefaults_RegistersDatabaseMigrationBeforeOutboxRelay()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=endatix_test;Username=postgres;Password=postgres",
                ["ConnectionStrings:DefaultConnection_DbProvider"] = "PostgreSql"
            })
            .Build();

        ServiceCollection services = new();
        services.AddSingleton(configuration);

        EndatixBuilder builder = new(services, configuration);

        // Act
        builder.Persistence.UseDefaults(DatabaseProvider.PostgreSql);

        // Assert
        var hostedServiceTypes = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType!)
            .ToList();

        var migrationIndex = hostedServiceTypes.IndexOf(typeof(DatabaseMigrationService));
        var seedingIndex = hostedServiceTypes.IndexOf(typeof(DataSeedingService));
        var outboxIndex = hostedServiceTypes.IndexOf(typeof(OutboxRelayBackgroundService));

        migrationIndex.Should().BeGreaterOrEqualTo(0);
        seedingIndex.Should().BeGreaterThan(migrationIndex);
        outboxIndex.Should().BeGreaterThan(seedingIndex);
        hostedServiceTypes.Count(type => type == typeof(OutboxRelayBackgroundService)).Should().Be(1);
    }
}
