using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Endatix.Infrastructure.Data;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class DatabaseMutationTests
{
    private readonly EndatixIntegrationWebHostFixture _fixture;

    public DatabaseMutationTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Respawn_resets_database_after_mutation()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Checkpoint.ResetAsync(
            _fixture.Database.ConnectionString,
            _fixture.Database.Provider,
            cancellationToken);

        int before;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            before = await db.Forms.CountAsync(cancellationToken);
        }

        try
        {
            using (var scope = _fixture.Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Tenant tenant = new("integration-mutation-tenant");
                db.Set<Tenant>().Add(tenant);
                await db.SaveChangesAsync(cancellationToken);
                Form form = new(tenant.Id, "integration-test-form");
                db.Forms.Add(form);
                await db.SaveChangesAsync(cancellationToken);
            }

            using (var scope = _fixture.Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var afterInsert = await db.Forms.CountAsync(cancellationToken);
                Assert.Equal(before + 1, afterInsert);
            }
        }
        finally
        {
            await _fixture.Checkpoint.ResetAsync(
                _fixture.Database.ConnectionString,
                _fixture.Database.Provider,
                cancellationToken);
        }

        // Assert
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var afterReset = await db.Forms.CountAsync(cancellationToken);
            Assert.Equal(before, afterReset);
        }
    }
}
