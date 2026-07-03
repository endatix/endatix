using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class PerTestSeedingTests
{
    private readonly EndatixIntegrationWebHostFixture _fixture;

    public PerTestSeedingTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Per_test_seed_builder_creates_user_role_and_form()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        const string roleName = "seeded-role";
        const string userName = "seeded-user";
        const string email = "seeded-user@test.local";
        const string formName = "seeded-form";

        await _fixture.Checkpoint.ResetAsync(
            _fixture.Database.ConnectionString,
            _fixture.Database.Provider,
            cancellationToken);

        long tenantId;
        using (var tenantScope = _fixture.Factory.Services.CreateScope())
        {
            var db = tenantScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Tenant tenant = new("integration-seed-tenant");
            db.Set<Tenant>().Add(tenant);
            await db.SaveChangesAsync(cancellationToken);
            tenantId = tenant.Id;
        }

        // Act
        await _fixture.Seed.SeedRoleAsync(tenantId, roleName, cancellationToken);
        await _fixture.Seed.SeedUserAsync(tenantId, userName, email, cancellationToken: cancellationToken);
        await _fixture.Seed.SeedFormAsync(tenantId, formName, cancellationToken);

        // Assert
        using var scope = _fixture.Factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hasRole = await identityDb.Roles.AnyAsync(x => x.TenantId == tenantId && x.Name == roleName, cancellationToken);
        var hasUser = await identityDb.Users.AnyAsync(x => x.TenantId == tenantId && x.UserName == userName, cancellationToken);
        var hasForm = await appDb.Forms.AnyAsync(x => x.TenantId == tenantId && x.Name == formName, cancellationToken);

        Assert.True(hasRole);
        Assert.True(hasUser);
        Assert.True(hasForm);
    }
}
