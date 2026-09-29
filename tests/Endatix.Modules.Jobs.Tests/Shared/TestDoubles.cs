using Endatix.Core.Abstractions;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Endatix.Modules.Jobs.Tests.Shared;

/// <summary>
/// A provider-agnostic context for unit tests. The real contexts are provider-split so each owns a
/// migration snapshot; that split is irrelevant in memory, and applying either one's provider
/// configuration would fail because the InMemory provider has no <c>jsonb</c> or filtered indexes.
/// </summary>
internal sealed class TestJobsDbContext(
    DbContextOptions<TestJobsDbContext> options,
    ITenantContext tenantContext)
    : JobsDbContextBase(options, tenantContext)
{
    public int SaveChangesCallCount { get; private set; }

    /// <summary>
    /// In-memory options that accept the transaction enqueueing opens. The provider cannot roll back, which the
    /// unit tests never rely on; the integration tests prove the rollback against PostgreSQL.
    /// </summary>
    public static DbContextOptions<TestJobsDbContext> InMemoryOptions(params IInterceptor[] interceptors) =>
        new DbContextOptionsBuilder<TestJobsDbContext>()
            .UseInMemoryDatabase($"jobs-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(interceptors)
            .Options;

    protected override void ApplyProviderConfigurations(ModelBuilder modelBuilder)
    {
        // No provider specifics in memory.
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return base.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class FixedTenantContext(long tenantId) : ITenantContext
{
    public long TenantId { get; } = tenantId;
}
