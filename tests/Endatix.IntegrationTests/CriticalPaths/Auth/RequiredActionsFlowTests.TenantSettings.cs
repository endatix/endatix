using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Tenant_settings_and_branding_check_standing_before_product_work()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var provider = new MutableProvider("terms", "/terms") { Pending = false };
        await using var host = new BrandingWebHost(fixture, configure: services =>
        {
            services.AddSingleton<IRequiredActionProvider>(provider);
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("help", "/help") { Key = "help", Severity = RequiredActionSeverity.Notice });
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        await EnsureTenantSettingsAsync(world, ct);
        using var published = await admin.SendAsync(PublishBrand(), ct);
        published.EnsureSuccessStatusCode();
        provider.Pending = true;
        await InvalidateAsync(world, ct);
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        Assert.Equal(2, me.GetProperty("requiredActions").GetArrayLength());

        // Act / Assert
        await AssertTenantOperationsRefusedAsync(admin, me, ct);

        using var anonymous = host.CreateClient();
        var slug = IntegrationSeedBuilder.StableShortUrl(world.Tenants[0].Name);
        var tenant = await anonymous.GetFromJsonAsync<JsonElement>($"/api/public/tenants/{slug}", ct);
        Assert.Equal("#0052d1", tenant.GetProperty("branding").GetProperty("tokens").GetProperty("color").GetProperty("primary").GetProperty("$value").GetString());

        provider.Pending = false;
        await InvalidateAsync(world, ct);
        var settings = await admin.GetFromJsonAsync<JsonElement>("/api/tenant-settings", ct);
        Assert.False(settings.GetProperty("requireFolderAssignment").GetBoolean());
        using var brand = await admin.GetAsync("/api/tenant-branding", ct);
        Assert.Equal("\"1\"", brand.Headers.ETag?.Tag);
    }

    private static async Task AssertTenantOperationsRefusedAsync(HttpClient admin, JsonElement me, CancellationToken ct)
    {
        foreach (var operation in TenantOperations())
        {
            using var request = operation;
            using var response = await admin.SendAsync(request, ct);
            await AssertStandingRefusalAsync(response, me, ct);
            Assert.Null(response.Headers.ETag);
        }
    }

    private static IEnumerable<HttpRequestMessage> TenantOperations()
    {
        yield return new(HttpMethod.Get, "/api/tenant-settings");
        yield return new(HttpMethod.Patch, "/api/tenant-settings") { Content = JsonContent.Create(new { requireFolderAssignment = true }) };
        yield return new(HttpMethod.Get, "/api/tenant-branding");
        yield return new(HttpMethod.Get, "/api/tenant-branding/versions");
        foreach (var operation in new[] { "published", "reset", "restore", "rollback" })
        {
            var request = new HttpRequestMessage(operation == "published" ? HttpMethod.Put : HttpMethod.Post, $"/api/tenant-branding/{operation}");
            request.Headers.IfMatch.ParseAdd("\"1\"");
            request.Content = operation == "restore" ? JsonContent.Create(new { number = 1 }) : JsonContent.Create(new { version = 1 });
            yield return request;
        }
    }

    private static HttpRequestMessage PublishBrand()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/tenant-branding/published");
        request.Headers.IfMatch.ParseAdd("\"0\"");
        request.Content = new StringContent("""{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#0052d1"}}}}""",
            System.Text.Encoding.UTF8, "application/json");
        return request;
    }

    [Fact]
    public async Task Ordinary_tenant_denials_take_precedence_over_unavailable_standing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var conflict = new MutableProvider("conflict", "/other");
        await using var host = new BrandingWebHost(fixture, entitled: false, configure: services =>
        {
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("terms", "/terms") { Severity = RequiredActionSeverity.Notice });
            services.AddSingleton<IRequiredActionProvider>(conflict);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var reader = await RestrictedUserAsync(world, ct);

        // Act / Assert
        using var permission = await reader.GetAsync("/api/tenant-branding/versions", ct);
        Assert.Equal(HttpStatusCode.Forbidden, permission.StatusCode);
        Assert.DoesNotContain("requiredActions", await permission.Content.ReadAsStringAsync(ct));
        using var noEntitlement = await admin.SendAsync(PublishBrand(), ct);
        Assert.Equal(HttpStatusCode.Forbidden, noEntitlement.StatusCode);
        Assert.Contains("plan does not include branding", await noEntitlement.Content.ReadAsStringAsync(ct));
        using var missing = await admin.GetAsync("/api/tenant-settings", ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await EnsureTenantSettingsAsync(world, ct);
        using var retry = await admin.PatchAsJsonAsync("/api/tenant-settings", new { requireFolderAssignment = true }, ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, retry.StatusCode);
        Assert.Contains("Try again", await retry.Content.ReadAsStringAsync(ct));

        conflict.Pending = false;
        await InvalidateAsync(world, ct);
        var settings = await admin.GetFromJsonAsync<JsonElement>("/api/tenant-settings", ct);
        Assert.False(settings.GetProperty("requireFolderAssignment").GetBoolean());
        var brand = await admin.GetFromJsonAsync<JsonElement>("/api/tenant-branding", ct);
        Assert.False(brand.GetProperty("entitled").GetBoolean());
    }

    private static async Task EnsureTenantSettingsAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = world.Tenants[0].Id;
        if (!await db.TenantSettings.AnyAsync(s => s.TenantId == tenant, ct))
        {
            db.TenantSettings.Add(new TenantSettings(tenant));
            await db.SaveChangesAsync(ct);
        }
    }

    [Fact]
    public async Task Unavailable_standing_allows_only_the_settings_existence_check()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var branding = Substitute.For<ITenantBrandingRepository>();
        var settings = Substitute.For<IRepository<TenantSettings>>();
        var cache = Substitute.For<IPublicTenantCacheInvalidator>();
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthorizationData { RequiredActionsUnavailable = true }));
        settings.AnyAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>()).Returns(true);
        await using var host = new BrandingWebHost(fixture, configure: services =>
        {
            services.AddSingleton(branding);
            services.AddSingleton(settings);
            services.AddSingleton(cache);
            services.AddScoped(_ => new AccountStanding(snapshots));
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);

        // Act
        foreach (var operation in TenantOperations())
        {
            using var request = operation;
            using var response = await admin.SendAsync(request, ct);
            Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable, Operation(response));
            await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
            snapshots.ClearReceivedCalls();
        }

        // Assert
        Assert.Empty(branding.ReceivedCalls());
        Assert.Empty(cache.ReceivedCalls());
        Assert.Equal([nameof(settings.AnyAsync), nameof(settings.AnyAsync)],
            settings.ReceivedCalls().Select(call => call.GetMethodInfo().Name));
    }
}
