using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Features.Email;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.FeatureFlows.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    private static readonly string[] _adminInviteRoles = ["Admin"];

    [Fact]
    public async Task Tenant_administration_resumes_in_the_same_session_after_standing_clears()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new MutableProvider("legal", "/legal") { Pending = false };
        var mail = Substitute.For<IEmailSender>();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton(mail);
            services.AddSingleton<IRequiredActionProvider>(provider);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var waiting = await InvitePendingAsync(admin, ct);
        await world.Seed.SeedRoleAsync(world.Tenants[0].Id, "StandingTest", ct);
        await world.Seed.SeedRolePermissionsAsync(world.Tenants[0].Id, "StandingTest", [Actions.Access.Hub], ct);
        var requests = AdministrationRequests(waiting, world.Tenants[0].Creator.Id).ToArray();
        var before = await IdentityStateAsync(world, ct);
        mail.ClearReceivedCalls();
        provider.Pending = true;
        await InvalidateAsync(world, ct);
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);

        foreach (var request in requests)
        {
            using var refused = await request(admin, ct);
            await AssertStandingRefusalAsync(refused, me, ct);
        }
        Assert.Equal(before, await IdentityStateAsync(world, ct));
        Assert.Empty(mail.ReceivedCalls());

        provider.Pending = false;
        await InvalidateAsync(world, ct);
        await AssertRecoveryAsync(admin, requests, ct);
    }

    private static async Task AssertRecoveryAsync(HttpClient admin, IEnumerable<Func<HttpClient, CancellationToken, Task<HttpResponseMessage>>> requests, CancellationToken ct)
    {
        foreach (var request in requests)
        {
            using var allowed = await request(admin, ct);
            Assert.True(allowed.IsSuccessStatusCode, Operation(allowed));
            if (allowed.RequestMessage is { Method.Method: "PUT", RequestUri.AbsolutePath: "/api/roles/StandingTest" })
            {
                var roles = await admin.GetFromJsonAsync<JsonElement>("/api/roles", ct);
                var updated = Assert.Single(roles.GetProperty("items").EnumerateArray(), r => r.GetProperty("name").GetString() == "StandingTest");
                Assert.Equal("Recovered role", updated.GetProperty("description").GetString());
            }
        }
    }

    private static async Task<string> InvitePendingAsync(HttpClient admin, CancellationToken ct)
    {
        using var invited = await admin.PostAsJsonAsync("/api/users", new { email = "waiting@example.test" }, ct);
        invited.EnsureSuccessStatusCode();
        return (await invited.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").ToString();
    }

    private static string Operation(HttpResponseMessage response) =>
        $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath}: {response.StatusCode}";

    private static async Task AssertStandingRefusalAsync(HttpResponseMessage refused, JsonElement me, CancellationToken ct)
    {
        Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, Operation(refused));
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.True(problem.TryGetProperty("errorCode", out var code) && code.GetString() == "required_action", Operation(refused));
        Assert.True(problem.TryGetProperty("requiredActions", out var actions) && JsonElement.DeepEquals(me.GetProperty("requiredActions"), actions), Operation(refused));
    }

    [Fact]
    public async Task Admitted_invites_preserve_role_validation_and_a_notice_allows_invitation()
    {
        var ct = TestContext.Current.CancellationToken;
        var conflict = new MutableProvider("conflict", "/other") { Pending = false };
        var mail = Substitute.For<IEmailSender>();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton(mail);
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("legal", "/legal") { Severity = RequiredActionSeverity.Notice });
            services.AddSingleton<IRequiredActionProvider>(conflict);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var before = await IdentityStateAsync(world, ct);
        using var invalid = await admin.PostAsJsonAsync("/api/users", new { email = "next@example.test", roles = _adminInviteRoles }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var existing = await admin.PostAsJsonAsync("/api/users", new { email = world.Tenants[0].Admin.Email }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, existing.StatusCode);
        Assert.Contains("already belongs to this tenant", await existing.Content.ReadAsStringAsync(ct));
        conflict.Pending = true;
        await InvalidateAsync(world, ct);
        using var retry = await admin.PostAsJsonAsync("/api/users", new { email = "next@example.test" }, ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, retry.StatusCode);
        var problem = await retry.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Contains("Try again", problem.GetProperty("detail").GetString());
        Assert.Equal(before, await IdentityStateAsync(world, ct));
        Assert.Empty(mail.ReceivedCalls());

        conflict.Pending = false;
        await InvalidateAsync(world, ct);
        using var recovered = await admin.PostAsJsonAsync("/api/users", new { email = "next@example.test" }, ct);
        recovered.EnsureSuccessStatusCode();
        await mail.Received(1).SendEmailAsync(Arg.Any<EmailWithTemplate>(), Arg.Any<CancellationToken>());
    }

    private static async Task<string> IdentityStateAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        return JsonSerializer.Serialize(new
        {
            Users = await db.Users.OrderBy(u => u.Id).Select(u => new { u.Id, u.TenantId, u.ConcurrencyStamp }).ToListAsync(ct),
            Roles = await db.Roles.OrderBy(r => r.Id).Select(r => new { r.Id, r.Description }).ToListAsync(ct),
            Grants = await db.UserRoles.OrderBy(g => g.UserId).ThenBy(g => g.RoleId).ToListAsync(ct),
            Permissions = await db.RolePermissions.OrderBy(g => g.RoleId).ThenBy(g => g.PermissionId).Select(g => new { g.RoleId, g.PermissionId }).ToListAsync(ct),
            Tokens = await db.EmailVerificationTokens.OrderBy(t => t.Id).Select(t => new { t.Id, t.IsUsed }).ToListAsync(ct)
        });
    }

    [Fact]
    public async Task Admitted_user_manager_gets_the_ordinary_denial_when_removing_a_more_privileged_user()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("legal", "/legal") { Severity = RequiredActionSeverity.Notice });
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" }, ct);
        using var manager = await RoleGrantCeilingFlowTests.UserManagerClientAsync(world, ct);
        var before = await IdentityStateAsync(world, ct);
        using var denied = await manager.DeleteAsync($"/api/users/{world.Tenants[0].Admin.Id}", ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var problem = await denied.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Contains("You can only grant access you hold yourself.", problem.GetProperty("detail").GetString());
        Assert.False(problem.TryGetProperty("requiredActions", out _));
        Assert.Equal(before, await IdentityStateAsync(world, ct));
    }

    private static IEnumerable<Func<HttpClient, CancellationToken, Task<HttpResponseMessage>>> AdministrationRequests(string waiting, long creator)
    {
        var user = $"/api/users/{creator}";
        var pending = $"/api/users/{waiting}";
        var role = new { name = "StandingNew", description = "Recovered role", permissions = new[] { Actions.Access.Hub } };
        yield return (c, ct) => c.GetAsync("/api/users", ct);
        yield return (c, ct) => c.GetAsync(user, ct);
        yield return (c, ct) => c.GetAsync("/api/roles", ct);
        yield return (c, ct) => c.GetAsync("/api/permissions", ct);
        yield return (c, ct) => c.PostAsJsonAsync("/api/users", new { email = "next@example.test" }, ct);
        yield return (c, ct) => c.PostAsync($"{pending}/resend-verification", null, ct);
        yield return (c, ct) => c.DeleteAsync($"{pending}/invite", ct);
        yield return (c, ct) => c.PostAsJsonAsync("/api/roles", role, ct);
        yield return (c, ct) => c.PutAsJsonAsync("/api/roles/StandingTest", role, ct);
        yield return (c, ct) => c.DeleteAsync("/api/roles/StandingTest", ct);
        yield return (c, ct) => c.PutAsJsonAsync($"{user}/roles", new { roleNames = Array.Empty<string>() }, ct);
        yield return (c, ct) => c.DeleteAsync(user, ct);
    }
}
