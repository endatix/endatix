extern alias EndatixWebHost;

using System.Buffers.Binary;
using System.Net.Http.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class AuthLoginFlowTests
{
    private const string SEED_PASSWORD = "Password123!";
    private readonly EndatixIntegrationWebHostFixture _fixture;

    public AuthLoginFlowTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_and_me_return_expected_roles_and_permissions_for_seeded_role_users()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        var world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SEED_PASSWORD },
            cancellationToken);

        var tenant = world.Tenants[0];

        // Act & Assert — TenantAdmin
        using var adminClient = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.Login, cancellationToken: cancellationToken);
        var adminData = await GetMeAsync(adminClient, cancellationToken);
        Assert.Equal(tenant.Id, adminData.TenantId);
        Assert.Contains(SystemRole.Authenticated.Name, adminData.Roles);
        Assert.Contains(SystemRole.Admin.Name, adminData.Roles);
        Assert.Contains(Actions.Access.Authenticated, adminData.Permissions);
        Assert.Contains(Actions.Access.Hub, adminData.Permissions);
        Assert.True(adminData.IsAdmin);

        // Act & Assert — Creator
        using var creatorClient = await world.AsAsync(TestPersona.Creator, mode: IntegrationAuthMode.Login, cancellationToken: cancellationToken);
        var creatorData = await GetMeAsync(creatorClient, cancellationToken);
        Assert.Equal(tenant.Id, creatorData.TenantId);
        Assert.Contains(SystemRole.Creator.Name, creatorData.Roles);
        Assert.Contains(Actions.Forms.Create, creatorData.Permissions);

        // Act & Assert — PlatformAdmin
        using var platformAdminClient = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.Login, cancellationToken: cancellationToken);
        var platformAdminData = await GetMeAsync(platformAdminClient, cancellationToken);
        Assert.Equal(tenant.Id, platformAdminData.TenantId);
        Assert.Contains(SystemRole.PlatformAdmin.Name, platformAdminData.Roles);
        Assert.True(platformAdminData.IsAdmin);
    }

    [Fact]
    public async Task Shared_host_hashes_passwords_and_refresh_tokens_at_the_fast_iteration_count()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD },
            cancellationToken);

        // Act
        using var client = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.Login, cancellationToken: cancellationToken);

        // Assert
        var (passwordHash, refreshTokenHash) = await SignedInUserHashesAsync(world.Services, cancellationToken);
        Assert.Equal(IntegrationPasswordHashing.FAST_ITERATION_COUNT, IterationCount(passwordHash));
        Assert.Equal(IntegrationPasswordHashing.FAST_ITERATION_COUNT, IterationCount(refreshTokenHash));
    }

    [Fact]
    public async Task Login_and_me_work_on_the_production_password_hasher_settings()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = new ProductionPasswordHashingHost(_fixture);
        var productionIterationCount = new PasswordHasherOptions().IterationCount;
        Assert.Equal(productionIterationCount, host.Services.GetRequiredService<IOptions<PasswordHasherOptions>>().Value.IterationCount);

        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD },
            cancellationToken);

        // Act
        using var client = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.Login, cancellationToken: cancellationToken);
        var me = await GetMeAsync(client, cancellationToken);

        // Assert
        Assert.Equal(world.Tenants[0].Id, me.TenantId);
        var (passwordHash, refreshTokenHash) = await SignedInUserHashesAsync(world.Services, cancellationToken);
        Assert.Equal(productionIterationCount, IterationCount(passwordHash));
        Assert.Equal(productionIterationCount, IterationCount(refreshTokenHash));
    }

    private static async Task<(string PasswordHash, string RefreshTokenHash)> SignedInUserHashesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(x => x.RefreshTokenHash != null, cancellationToken);
        return (user.PasswordHash!, user.RefreshTokenHash!);
    }

    /// <summary>The count an Identity V3 hash records: format marker, PRF and iteration count, big-endian.</summary>
    private static int IterationCount(string hash) =>
        (int)BinaryPrimitives.ReadUInt32BigEndian(Convert.FromBase64String(hash).AsSpan(5, 4));

    private static async Task<AuthorizationData> GetMeAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var meResponse = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), cancellationToken);
        meResponse.EnsureSuccessStatusCode();

        var mePayload = await meResponse.Content.ReadFromJsonAsync<AuthorizationData>(cancellationToken);
        Assert.NotNull(mePayload);
        return mePayload;
    }

    /// <summary>A host on the collection's database with production password hashing. The world is seeded on it too.</summary>
    private sealed class ProductionPasswordHashingHost : IIntegrationTestHostFixture, IAsyncDisposable
    {
        private readonly WebApplicationFactory<EndatixWebHost::Program> _factory;

        public ProductionPasswordHashingHost(EndatixIntegrationWebHostFixture fixture)
        {
            Database = fixture.Database;
            _factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider)
            {
                PasswordHashing = PasswordHashing.Production,
            };

            // Reading Services starts the host.
            Seed = new IntegrationSeedBuilder(_factory.Services);
        }

        public DatabaseInfrastructureFixture Database { get; }

        public DatabaseCheckpoint Checkpoint => Database.Checkpoint;

        public IServiceProvider Services => _factory.Services;

        public IntegrationSeedBuilder Seed { get; }

        public HttpClient CreateClient() => _factory.CreateClient();

        public ValueTask DisposeAsync() => _factory.DisposeAsync();
    }
}
