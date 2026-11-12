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
/// Keycloak sign-in never changes a user's tenant. A user stored at another tenant, tenant 0 included, is refused and
/// nothing is written.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class ExternalUserTenantMismatchTests(EndatixIntegrationWebHostFixture fixture)
{
    private static readonly string[] _mappedRoles = ["Admin"];

    [Fact]
    public async Task User_at_tenant_0_is_refused_and_not_moved()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, cancellationToken);
        var identity = KeycloakIdentity.New();
        await SeedKeycloakUserAsync(world, tenantId: 0, identity);
        var before = await StoredUsersAsync(world, identity.Subject, cancellationToken);

        // Act
        var result = await ProvisionAsync(world, world.Tenants[0].Id, identity);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        (await StoredUsersAsync(world, identity.Subject, cancellationToken)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task User_at_another_tenant_is_refused_and_not_moved()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, cancellationToken);
        var otherTenantId = await IntegrationTenants.InsertAsync(fixture.Database, cancellationToken);
        var identity = KeycloakIdentity.New();
        await SeedKeycloakUserAsync(world, otherTenantId, identity);
        var before = await StoredUsersAsync(world, identity.Subject, cancellationToken);

        // Act
        var result = await ProvisionAsync(world, world.Tenants[0].Id, identity);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        (await StoredUsersAsync(world, identity.Subject, cancellationToken)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task User_whose_access_was_removed_stays_removed_on_the_next_sign_in()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" },
            cancellationToken);
        var tenantId = world.Tenants[0].Id;
        var identity = KeycloakIdentity.New();
        var signedIn = await ProvisionAsync(world, tenantId, identity);
        signedIn.IsSuccess.Should().BeTrue();
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        using var removed = await admin.DeleteAsync(new Uri($"/api/users/{signedIn.Value.Id}", UriKind.Relative), cancellationToken);
        removed.IsSuccessStatusCode.Should().BeTrue(await removed.Content.ReadAsStringAsync(cancellationToken));

        // Act
        var result = await ProvisionAsync(world, tenantId, identity);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        (await StoredUsersAsync(world, identity.Subject, cancellationToken))
            .Should().ContainSingle().Which.TenantId.Should().Be(0);
    }

    private static async Task SeedKeycloakUserAsync(IntegrationTestWorld world, long tenantId, KeycloakIdentity identity)
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

    private static async Task<List<StoredUser>> StoredUsersAsync(IntegrationTestWorld world, string subject, CancellationToken cancellationToken)
    {
        using var scope = world.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>().Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.AuthProvider == AuthProviders.Keycloak && user.ExternalSubjectId == subject)
            .Select(user => new StoredUser(user.Id, user.TenantId, user.ConcurrencyStamp, user.LastLoginAt))
            .ToListAsync(cancellationToken);
    }

    private sealed record StoredUser(long Id, long TenantId, string? ConcurrencyStamp, DateTimeOffset? LastLoginAt);

    private sealed record KeycloakIdentity(string Subject, string Email)
    {
        public static KeycloakIdentity New()
        {
            var subject = Guid.NewGuid().ToString();
            return new(subject, $"{subject}@example.com");
        }
    }
}
