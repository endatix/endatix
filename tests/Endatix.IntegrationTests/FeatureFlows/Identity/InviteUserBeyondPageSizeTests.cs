using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.Identity;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class InviteUserBeyondPageSizeTests
{
    private const string SeedPassword = "Password123!";
    private const string TargetRoleName = "ZzzInvite";

    private readonly EndatixIntegrationWebHostFixture _fixture;

    public InviteUserBeyondPageSizeTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Invite_WithRoleBeyondMaxPageSize_Succeeds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            ct);
        long tenantId = world.Tenants[0].Id;
        await SeedFillerRolesAndTargetAsync(world, tenantId, ct);

        using HttpClient client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/users", UriKind.Relative),
            new { email = $"invitee-{Guid.NewGuid():N}@endatix.com", roles = new[] { TargetRoleName } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        document.RootElement.GetProperty("roles")
            .EnumerateArray()
            .Select(role => role.GetString())
            .Should()
            .Contain(TargetRoleName);
    }

    private static async Task SeedFillerRolesAndTargetAsync(
        IntegrationTestWorld world,
        long tenantId,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = world.Services.CreateScope();
        AppIdentityDbContext identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        List<AppRole> roles = Enumerable.Range(0, PagedRequestLimits.MAX_PAGE_SIZE)
            .Select(index => new AppRole
            {
                TenantId = tenantId,
                Name = $"Aaa{index:000}",
                NormalizedName = $"AAA{index:000}",
                IsActive = true
            })
            .ToList();
        roles.Add(new AppRole
        {
            TenantId = tenantId,
            Name = TargetRoleName,
            NormalizedName = TargetRoleName.ToUpperInvariant(),
            IsActive = true
        });

        identityDb.Roles.AddRange(roles);
        await identityDb.SaveChangesAsync(cancellationToken);
    }
}
