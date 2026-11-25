using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Attributes;
using Endatix.Hosting;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.IntegrationTests.FeatureFlows.DataLists;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_exempt_repair_operation_still_requires_its_platform_permission(bool rawDispatch)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("profile", "/profile"));
            services.AddSingleton<IStartupFilter>(new GateTestRoutes(rawDispatch));
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var anonymous = host.CreateClient();
        using var member = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);

        using var challenge = await anonymous.GetAsync(GateTestRoutes.REPAIR, ct);
        using var forbid = await member.GetAsync(GateTestRoutes.REPAIR, ct);
        using var allowed = await admin.GetAsync(GateTestRoutes.REPAIR, ct);
        using var gated = await admin.GetAsync(GateTestRoutes.NEIGHBOR, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbid.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        await AssertStandingRefusalAsync(gated, await admin.GetFromJsonAsync<JsonElement>("/api/auth/me", ct), ct);
    }

    [Fact]
    public async Task A_direct_policy_combined_with_form_token_authorization_is_gated()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IStartupFilter>(new GateTestRoutes(false));
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        var (form, _) = await FormAccessTokenUseFlowTests.SeedRuntimeFormAsync(world, true, ct);
        using var respondent = await PublicDataListScopeFlowTests.AnonymousWithFormTokenAsync(world, form, ct);

        using var mixed = await respondent.GetAsync(GateTestRoutes.MIXED, ct);

        await AssertUnavailableQuestionsAsync(mixed, ct);
    }

    [Fact]
    public async Task A_pending_account_keeps_framework_method_and_media_type_rejections()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IStartupFilter>(new GateTestRoutes(false));
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("profile", "/profile"));
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var plain = new StringContent("ordinary text", Encoding.UTF8, "text/plain");

        using var wrongMethod = await admin.GetAsync(GateTestRoutes.JSON, ct);
        using var wrongMedia = await admin.PostAsync(GateTestRoutes.JSON, plain, ct);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongMedia.StatusCode);
    }

    private sealed class GateTestRoutes(bool rawDispatch) : IStartupFilter
    {
        internal const string REPAIR = "/api/__integration-test__/repair";
        internal const string NEIGHBOR = "/api/__integration-test__/ordinary";
        internal const string MIXED = "/api/__integration-test__/mixed";
        internal const string JSON = "/api/__integration-test__/json";
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.UseRouting();
            if (rawDispatch)
            {
                app.UseEndatix(builder =>
                {
                    builder.UseSecurity();
                    builder.App.UseRequiredActionsGate().UseEndpoints(MapRoutes);
                });
                return;
            }
            app.UseEndatix(builder => builder.UseSecurity());
            app.UseEndpoints(MapRoutes);
        };

        private static void MapRoutes(IEndpointRouteBuilder routes)
        {
            routes.MapGet(REPAIR, () => Results.NoContent()).RequireAuthorization(AuthorizationPolicies.PlatformAdminAccess)
                .WithMetadata(new AllowWithRequiredActionsAttribute());
            routes.MapGet(NEIGHBOR, () => Results.NoContent()).RequireAuthorization(AuthorizationPolicies.PlatformAdminAccess);
            routes.MapGet(MIXED, () => Results.NoContent()).RequireAuthorization(AuthorizationPolicies.PublicResourceAccess)
                .RequireAuthorization(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
            routes.MapPost(JSON, (JsonElement body) => Results.Ok(body)).RequireAuthorization(AuthorizationPolicies.PlatformAdminAccess);
        }
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
