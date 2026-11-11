using System.Runtime.CompilerServices;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Hosting;

namespace Endatix.IntegrationTests;

/// <summary>
/// Every fixture database of this assembly starts with every module's migrations applied: the template is built by a
/// host with every module on, Reporting included, which the shared host leaves off.
/// </summary>
internal static class MigratedDatabaseTemplate
{
    [ModuleInitializer]
    internal static void Register() => IntegrationDatabaseTemplate.Use(MigrateAsync);

    // Starting the host applies the migrations; its background work stays off, so it leaves no scheduler or outbox rows.
    internal static async Task MigrateAsync(string connectionString, TestDatabaseProvider provider, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var factory = new EndatixWebApplicationFactory(connectionString, provider)
            .WithWebHostBuilder(builder => builder.UseSetting("Endatix:FeatureFlags:ReportingModule", "true"));
        _ = factory.Services;
    }
}
