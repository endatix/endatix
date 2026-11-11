using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Provisioning;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// Existing users that Keycloak sign-ins provisioned at tenant 0 move to the configured tenant on their next sign-in;
/// a user at another tenant stays where it is.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class ExternalUserTenantMoveTests(EndatixIntegrationWebHostFixture fixture)
{
    private static readonly string[] _mappedRoles = ["Admin"];

    [Fact]
    public async Task User_at_tenant_0_moves_to_the_configured_tenant_once()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, cancellationToken);
        var tenantId = world.Tenants[0].Id;
        var identity = KeycloakIdentity.New();
        var existingUserId = await SeedKeycloakUserAsync(world, tenantId: 0, identity);

        // Act
        var first = await ProvisionAsync(world, tenantId, identity);
        var second = await ProvisionAsync(world, tenantId, identity);

        // Assert
        first.IsSuccess.Should().BeTrue();
        first.Value.Id.Should().Be(existingUserId);
        first.Value.TenantId.Should().Be(tenantId);
        second.IsSuccess.Should().BeTrue();
        second.Value.Id.Should().Be(existingUserId);
        (await TenantsOfAsync(world, identity.Subject, cancellationToken)).Should().Equal(tenantId);
    }

    [Fact]
    public async Task User_at_another_tenant_is_not_moved()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, cancellationToken);
        var tenantId = world.Tenants[0].Id;
        var otherTenantId = await IntegrationTenants.InsertAsync(fixture.Database, cancellationToken);
        var identity = KeycloakIdentity.New();
        await SeedKeycloakUserAsync(world, otherTenantId, identity);

        // Act
        var result = await ProvisionAsync(world, tenantId, identity);

        // Assert
        result.IsSuccess.Should().BeFalse();
        (await TenantsOfAsync(world, identity.Subject, cancellationToken)).Should().Equal(otherTenantId);
    }

    [Fact]
    public async Task Move_into_a_tenant_where_the_email_is_taken_is_a_conflict()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, cancellationToken);
        var tenant = world.Tenants[0];
        var identity = KeycloakIdentity.New() with { Email = tenant.Admin.Email };
        await SeedKeycloakUserAsync(world, tenantId: 0, identity);

        // Act
        var result = await ProvisionAsync(world, tenant.Id, identity);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        (await TenantsOfAsync(world, identity.Subject, cancellationToken)).Should().Equal(0L);
    }

    private static async Task<long> SeedKeycloakUserAsync(IntegrationTestWorld world, long tenantId, KeycloakIdentity identity)
    {
        using var scope = world.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        AppUser user = new()
        {
            TenantId = tenantId,
            AuthProvider = AuthProviders.Keycloak,
            ExternalSubjectId = identity.Subject,
            UserName = ExternalAppUserProvisioner.BuildExternalUserName(AuthProviders.Keycloak, identity.Subject),
            Email = identity.Email,
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        var created = await userManager.CreateAsync(user);
        created.Succeeded.Should().BeTrue(string.Join("; ", created.Errors.Select(error => error.Description)));
        return user.Id;
    }

    private static async Task<Result<AppUser>> ProvisionAsync(IntegrationTestWorld world, long tenantId, KeycloakIdentity identity)
    {
        using var scope = world.Services.CreateScope();
        ExternalAppUserProvisioner provisioner = new(
            scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>(),
            scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
            NullLogger<ExternalAppUserProvisioner>.Instance);

        return await provisioner.ProvisionAsync(
            tenantId,
            AuthProviders.Keycloak,
            identity.Subject,
            _mappedRoles,
            new ExternalIdentityProfile(identity.Email, "Keycloak user"),
            TestContext.Current.CancellationToken);
    }

    private static async Task<List<long>> TenantsOfAsync(IntegrationTestWorld world, string subject, CancellationToken cancellationToken)
    {
        using var scope = world.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>().Users
            .AsNoTracking()
            .Where(user => user.AuthProvider == AuthProviders.Keycloak && user.ExternalSubjectId == subject)
            .Select(user => user.TenantId)
            .ToListAsync(cancellationToken);
    }

    private sealed record KeycloakIdentity(string Subject, string Email)
    {
        public static KeycloakIdentity New()
        {
            var subject = Guid.NewGuid().ToString();
            return new(subject, $"{subject}@example.com");
        }
    }
}
