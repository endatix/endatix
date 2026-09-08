using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Jobs.Tests.Shared;

/// <summary>
/// A provider-agnostic context for unit tests. The real contexts are provider-split so each owns a
/// migration snapshot; that split is irrelevant in memory, and applying either one's provider
/// configuration would fail because the InMemory provider has no <c>jsonb</c> or filtered indexes.
/// </summary>
internal sealed class TestJobsDbContext(
    DbContextOptions<TestJobsDbContext> options,
    IIdGenerator<long> idGenerator,
    ITenantContext tenantContext)
    : JobsDbContextBase(options, tenantContext, new EfCoreValueGeneratorFactory(idGenerator))
{
    public int SaveChangesCallCount { get; private set; }

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

/// <summary>Predictable ids, so assertions can compare them without ordering surprises.</summary>
internal sealed class SequentialIdGenerator : IIdGenerator<long>
{
    private long _current;

    public long CreateId() => Interlocked.Increment(ref _current);
}

internal sealed class FixedTenantContext(long tenantId) : ITenantContext
{
    public long TenantId { get; } = tenantId;
}
