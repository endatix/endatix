using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Endatix.Infrastructure.Data;

namespace Endatix.IntegrationTests;

[Collection(nameof(OssIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class DatabaseMutationTests
{
    private readonly OssIntegrationWebHostFixture _fixture;

    public DatabaseMutationTests(OssIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Respawn_resets_database_after_mutation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        int before;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            before = await db.Forms.CountAsync(cancellationToken);
        }

        Assert.True(before >= 0);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Tenant tenant = new("integration-mutation-tenant");
            db.Set<Tenant>().Add(tenant);
            await db.SaveChangesAsync(cancellationToken);
            Form form = new(tenant.Id, "integration-test-form");
            db.Forms.Add(form);
            await db.SaveChangesAsync(cancellationToken);
        }

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            int afterInsert = await db.Forms.CountAsync(cancellationToken);
            Assert.Equal(before + 1, afterInsert);
        }

        await _fixture.Checkpoint.ResetAsync(
            _fixture.Database.ConnectionString,
            _fixture.Database.Provider,
            cancellationToken);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            int afterReset = await db.Forms.CountAsync(cancellationToken);
            Assert.Equal(0, afterReset);
        }
    }
}
