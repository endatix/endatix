using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

/// <summary>Tenants written straight to the core schema, for tests that seed data per tenant.</summary>
internal static class IntegrationTenants
{
    /// <summary>Inserts a tenant with a unique name and returns its id.</summary>
    public static Task<long> InsertAsync(DatabaseInfrastructureFixture database, CancellationToken cancellationToken) =>
        InsertAsync(database, NewTenant(), cancellationToken);

    /// <summary>Inserts a soft-deleted tenant with a unique name and returns its id.</summary>
    public static Task<long> InsertDeletedAsync(DatabaseInfrastructureFixture database, CancellationToken cancellationToken)
    {
        var tenant = NewTenant();
        tenant.Delete();
        return InsertAsync(database, tenant, cancellationToken);
    }

    private static Tenant NewTenant() =>
        new($"tenant-{Guid.NewGuid():N}"[..32], Guid.NewGuid().ToString("N")[..8]);

    private static async Task<long> InsertAsync(
        DatabaseInfrastructureFixture database,
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigureOptions(optionsBuilder, database.ConnectionString, database.Provider);
        await using AppDbContext appDb = new(
            optionsBuilder.Options,
            IntegrationTenantContext.Bypass,
            new OutboxIntegrationEventDispatcher());

        appDb.Set<Tenant>().Add(tenant);
        await appDb.SaveChangesAsync(cancellationToken);
        return tenant.Id;
    }
}
