using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.InviteUser;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Admitted_administration_operations_use_only_the_gate_decision()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthorizationData { UserId = "42" }));
        // A fresh decision per resolution exposes duplicate consumers that a scoped admission would mask.
        await using var host = new ReportingWebHost(fixture, services =>
            services.AddTransient(_ => new AccountStanding(snapshots)));
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);

        foreach (var route in new[] { "/api/system/version", "/api/roles", "/api/users", "/api/admin/auth/settings" })
        {
            snapshots.ClearReceivedCalls();
            using var response = await admin.GetAsync(route, ct);
            response.EnsureSuccessStatusCode();
            await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        }

        snapshots.ClearReceivedCalls();
        using var created = await admin.PostAsJsonAsync("/api/roles", new { name = "Reviewers", permissions = new[] { Actions.Access.Hub } }, ct);
        created.EnsureSuccessStatusCode();
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Eligible_invites_call_custom_registration_once(bool enforced, bool notice)
    {
        const string email = "invited@example.test";
        var ct = TestContext.Current.CancellationToken;
        var registration = Substitute.For<IUserRegistrationService>();
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        var snapshot = AdmittedSnapshot() with
        {
            RequiredActions = notice ? [new() { Key = "legal", ResolvePath = "/legal", Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Notice }] : []
        };
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(snapshot));
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton(registration);
            if (enforced)
            {
                services.AddTransient(_ => new AccountStanding(snapshots));
            }
            if (notice)
            {
                var eligibility = Substitute.For<IInviteEligibilityProvider>();
                eligibility.DecideAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(InviteEligibility.Allowed);
                services.AddSingleton(eligibility);
            }
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        var tenant = world.Tenants[0];
        await world.Seed.SeedRoleAsync(tenant.Id, "Reviewer", ct);
        registration.RegisterInvitedUserAsync(email, tenant.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new User(tenant.Creator.Id, tenant.Id, email, email, false)));
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        snapshots.ClearReceivedCalls();
        using var valid = await admin.PostAsJsonAsync("/api/users", new { email, roles = new[] { "Reviewer" } }, ct);

        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        var invited = await valid.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Contains(invited.GetProperty("roles").EnumerateArray(), role => role.GetString() == "Reviewer");
        await registration.Received(1).RegisterInvitedUserAsync(email, tenant.Id, Arg.Any<CancellationToken>());
        await snapshots.Received(enforced ? 1 : 0).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Standing_and_ordinary_invite_refusals_never_call_custom_registration()
    {
        const string email = "invited@example.test";
        var ct = TestContext.Current.CancellationToken;
        var registration = Substitute.For<IUserRegistrationService>();
        var provider = new MutableProvider("legal", "/legal");
        var conflict = new MutableProvider("conflict", "/other") { Pending = false };
        var eligibility = Substitute.For<IInviteEligibilityProvider>();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton(registration);
            services.AddSingleton<IRequiredActionProvider>(provider);
            services.AddSingleton<IRequiredActionProvider>(conflict);
            services.AddSingleton(eligibility);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var refused = await admin.PostAsJsonAsync("/api/users", new { email }, ct);
        await AssertStandingRefusalAsync(refused, await admin.GetFromJsonAsync<JsonElement>("/api/auth/me", ct), ct);
        conflict.Pending = true;
        await InvalidateAsync(world, ct);
        using var unavailable = await admin.PostAsJsonAsync("/api/users", new { email }, ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Contains("Try again", await unavailable.Content.ReadAsStringAsync(ct));
        conflict.Pending = false;
        provider.Pending = false;
        await InvalidateAsync(world, ct);
        using var reader = await RestrictedUserAsync(world, ct);
        using var denied = await reader.PostAsJsonAsync("/api/users", new { email }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var invalid = await admin.PostAsJsonAsync("/api/users", new { email, roles = _adminInviteRoles }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Empty(registration.ReceivedCalls());
        Assert.Empty(eligibility.ReceivedCalls());
    }
}
