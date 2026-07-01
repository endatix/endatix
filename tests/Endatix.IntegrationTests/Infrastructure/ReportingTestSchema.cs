using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

internal static class ReportingTestSchema
{
    public static async Task EnsureMigratedAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString);
        ModuleDbContextExtensions.ConfigureProviderScopedMigrations(
            optionsBuilder,
            ReportingPersistence.PostgreSqlMigrationsNamespace);

        await using ReportingDbContext context = new(
            optionsBuilder.Options,
            new NoOpIdGenerator(),
            new BypassTenantContext());

        await context.Database.MigrateAsync(cancellationToken);
    }

    private sealed class NoOpIdGenerator : IIdGenerator<long>
    {
        public long CreateId() => 0;
    }

    private sealed class BypassTenantContext : ITenantContext
    {
        public long TenantId => 0;
    }
}
