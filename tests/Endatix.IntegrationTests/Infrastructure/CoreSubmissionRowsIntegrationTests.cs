using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

/// <summary>
/// The Reporting contexts' read-only <see cref="CoreSubmissionRow"/> mapping against a real database: the core
/// submissions are read through the same tenant and soft-delete query filters as every other tenant-owned row.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class CoreSubmissionRowsIntegrationTests(DbIntegrationFixture fixture)
{
    private const long TenantId = 81;
    private const long OtherTenantId = 82;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CoreSubmissionRows_WithAnotherTenantsSubmission_HidesIt()
    {
        // Arrange
        await ResetAsync();
        var own = await AddSubmissionAsync(TenantId);
        await AddSubmissionAsync(OtherTenantId);

        // Act
        var ids = await ReadIdsAsync(TenantId);

        // Assert
        ids.Should().Equal(own);
    }

    [Fact]
    public async Task CoreSubmissionRows_WithDeletedSubmission_HidesIt()
    {
        // Arrange
        await ResetAsync();
        var live = await AddSubmissionAsync(TenantId);
        await AddSubmissionAsync(TenantId, deleted: true);

        // Act
        var ids = await ReadIdsAsync(TenantId);

        // Assert
        ids.Should().Equal(live);
    }

    private async Task ResetAsync()
    {
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, Cancellation);
        await ReportingTestSchema.EnsureMigratedAsync(fixture.ConnectionString, fixture.Provider, Cancellation);
        foreach (var tenantId in new[] { TenantId, OtherTenantId })
        {
            await using var appDb = CreateAppDbContext(tenantId);
            appDb.Set<Tenant>().Add(new Tenant($"core-rows-{tenantId}", $"tnntcr{tenantId}") { Id = tenantId });
            await appDb.SaveChangesAsync(Cancellation);
        }
    }

    // The bare DbSet, with no tenant or deletion predicate of its own: only the query filters can hide a row.
    private async Task<List<long>> ReadIdsAsync(long tenantId)
    {
        await using var reportingDb = ReportingTestSchema.CreateContext(
            fixture.ConnectionString, fixture.Provider, new IntegrationTenantContext(tenantId));
        return await reportingDb.CoreSubmissionRows.Select(row => row.Id).ToListAsync(Cancellation);
    }

    private async Task<long> AddSubmissionAsync(long tenantId, bool deleted = false)
    {
        await using var appDb = CreateAppDbContext(tenantId);
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: $"core-rows-{Guid.NewGuid():N}"));
        appDb.Forms.Add(form);
        await appDb.SaveChangesAsync(Cancellation);
        FormDefinition definition = new(tenantId, isDraft: false, jsonData: """{"pages":[]}""");
        form.AddFormDefinition(definition);
        appDb.Set<FormDefinition>().Add(definition);
        await appDb.SaveChangesAsync(Cancellation);
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: tenantId,
            FormId: form.Id,
            FormDefinitionId: definition.Id,
            JsonData: "{}",
            IsComplete: true));
        appDb.Submissions.Add(submission);
        await appDb.SaveChangesAsync(Cancellation);
        if (deleted)
        {
            submission.Delete();
            await appDb.SaveChangesAsync(Cancellation);
        }

        return submission.Id;
    }

    private AppDbContext CreateAppDbContext(long tenantId)
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigureOptions(optionsBuilder, fixture.ConnectionString, fixture.Provider);
        return new AppDbContext(optionsBuilder.Options, new IntegrationTenantContext(tenantId), new OutboxIntegrationEventDispatcher());
    }
}
