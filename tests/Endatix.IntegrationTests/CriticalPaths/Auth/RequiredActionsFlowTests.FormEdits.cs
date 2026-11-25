using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task Clear_standing_preserves_first_validation_error_when_assignments_are_also_invalid(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var route = $"/api/forms/{await CreateFormAsync(owner, ct)}";
        using var enabled = await owner.PatchAsJsonAsync(route, new { limitOnePerUser = true }, ct);
        enabled.EnsureSuccessStatusCode();
        await using var host = new ReportingWebHost(fixture, EnableStanding);
        using var clear = host.CreateClient();
        clear.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;

        foreach (var client in new[] { owner, clear })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), route)
            {
                Content = JsonContent.Create(new { name = "Changed form", isEnabled = true, limitOnePerUser = false, folderId = "99999", themeId = 99999 })
            };
            using var result = await client.SendAsync(request, ct);
            Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
            var problem = await result.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal("Single submission gate cannot be disabled after it has been enabled.", problem.GetProperty("detail").GetString());
        }
    }

    [Fact]
    public async Task Policy_update_committed_during_standing_is_seen_before_create()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        await EnsureTenantSettingsAsync(world, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var snapshot = Substitute.For<ICurrentUserAuthorizationService>();
        snapshot.GetAuthorizationDataAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            using var change = await owner.PatchAsJsonAsync("/api/tenant-settings", new { requireFolderAssignment = true }, ct);
            change.EnsureSuccessStatusCode();
            return Result.Success(new AuthorizationData());
        });
        await using var host = new ReportingWebHost(fixture, services => services.AddScoped(_ => new AccountStanding(snapshot)));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        var before = await FormCountsAsync(world, ct);

        using var create = await ManagedFormRequestAsync(client, "create", "");

        Assert.Equal(HttpStatusCode.InternalServerError, create.StatusCode);
        Assert.Equal(before, await FormCountsAsync(world, ct));
        await snapshot.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
    }

    private static async Task<(int MatchingForms, int Definitions)> FormCountsAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = world.Tenants[0].Id;
        return (await db.Forms.CountAsync(f => f.TenantId == tenant && f.Name == "New form name", ct),
            await db.FormDefinitions.CountAsync(d => d.TenantId == tenant, ct));
    }
}
