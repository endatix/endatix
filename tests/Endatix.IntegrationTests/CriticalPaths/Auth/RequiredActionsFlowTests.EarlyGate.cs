using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Attributes;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task An_exempt_repair_operation_still_requires_its_platform_permission()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("profile", "/profile"));
            services.AddSingleton<IStartupFilter, RepairRoute>();
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var anonymous = host.CreateClient();
        using var member = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);

        using var challenge = await anonymous.GetAsync(RepairRoute.Path, ct);
        using var forbid = await member.GetAsync(RepairRoute.Path, ct);
        using var allowed = await admin.GetAsync(RepairRoute.Path, ct);
        using var gated = await admin.GetAsync(RepairRoute.Neighbor, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbid.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        await AssertStandingRefusalAsync(gated, await admin.GetFromJsonAsync<JsonElement>("/api/auth/me", ct), ct);
    }

    private sealed class RepairRoute : IStartupFilter
    {
        internal const string Path = "/api/__integration-test__/repair";
        internal const string Neighbor = "/api/__integration-test__/ordinary";
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.UseRouting();
            app.UseAuthorization();
            app.UseRequiredActionsGate();
            app.UseEndpoints(routes =>
            {
                routes.MapGet(Path, () => Results.NoContent()).RequireAuthorization(AuthorizationPolicies.PlatformAdminAccess)
                    .WithMetadata(new AllowWithRequiredActionsAttribute());
                routes.MapGet(Neighbor, () => Results.NoContent()).RequireAuthorization(AuthorizationPolicies.PlatformAdminAccess);
            });
        };
    }

    [Fact]
    public async Task Pending_owner_receives_the_me_actions_before_json_binding()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("profile", "/profile"));
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var me = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        using var incomplete = new StringContent("{", Encoding.UTF8, "application/json");

        // Act
        using var response = await owner.PostAsync("/api/forms", incomplete, ct);

        // Assert
        await AssertStandingRefusalAsync(response, me, ct);
    }
}
