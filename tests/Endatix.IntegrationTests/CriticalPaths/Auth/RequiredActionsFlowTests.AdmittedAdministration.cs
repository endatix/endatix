using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
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
    [InlineData(true, HttpStatusCode.ServiceUnavailable, 0)]
    [InlineData(false, HttpStatusCode.OK, 1)]
    public async Task Custom_registration_preserves_invite_admission_and_role_checks(bool enforced, HttpStatusCode expected, int dispatches)
    {
        const string email = "invited@example.test";
        var ct = TestContext.Current.CancellationToken;
        var registration = Substitute.For<IUserRegistrationService>();
        var provider = new MutableProvider("legal", "/legal");
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton(registration);
            services.AddSingleton<IRequiredActionProvider>(provider);
            if (enforced)
            {
                EnableStanding(services);
            }
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        var tenant = world.Tenants[0];
        registration.RegisterInvitedUserAsync(email, tenant.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new User(tenant.Creator.Id, tenant.Id, email, email, false)));
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        if (enforced)
        {
            using var refused = await admin.PostAsJsonAsync("/api/users", new { email }, ct);
            await AssertStandingRefusalAsync(refused, await admin.GetFromJsonAsync<JsonElement>("/api/auth/me", ct), ct);
        }
        provider.Pending = false;
        await InvalidateAsync(world, ct);
        using var reader = await RestrictedUserAsync(world, ct);
        using var denied = await reader.PostAsJsonAsync("/api/users", new { email }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var invalid = await admin.PostAsJsonAsync("/api/users", new { email, roles = _adminInviteRoles }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Empty(registration.ReceivedCalls());
        using var valid = await admin.PostAsJsonAsync("/api/users", new { email }, ct);
        Assert.Equal(expected, valid.StatusCode);
        await registration.Received(dispatches).RegisterInvitedUserAsync(email, world.Tenants[0].Id, Arg.Any<CancellationToken>());
    }
}
