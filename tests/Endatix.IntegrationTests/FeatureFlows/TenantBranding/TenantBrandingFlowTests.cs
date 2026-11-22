using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests.FeatureFlows.TenantBranding;

/// <summary>
/// The tenant branding API over HTTP (<c>tenant-branding</c>) and the brand on the public tenant read, on
/// <see cref="BrandingWebHost"/> (WhiteLabeling on) and on the shared host (off).
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class TenantBrandingFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const string BLUE = """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#0052d1"}}}}""";
    private const string GREEN = """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#16a34a"}}}}""";

    private static readonly Uri BrandingRoute = new("/api/tenant-branding", UriKind.Relative);
    private static readonly Uri PublishRoute = new("/api/tenant-branding/published", UriKind.Relative);
    private static readonly Uri RestoreRoute = new("/api/tenant-branding/restore", UriKind.Relative);
    private static readonly Uri RollbackRoute = new("/api/tenant-branding/rollback", UriKind.Relative);
    private static readonly Uri VersionsRoute = new("/api/tenant-branding/versions", UriKind.Relative);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_admin_publishes_rolls_back_and_restores_and_the_public_read_follows()
    {
        // Arrange
        await using BrandingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, Cancellation);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: Cancellation);
        using var anonymous = world.AnonymousClient();
        var slug = await ShortUrlAsync(world);

        // Act - read before anything is published, then publish twice
        using var initial = await admin.GetAsync(BrandingRoute, Cancellation);
        var initialTag = initial.Headers.ETag!.Tag;
        using var blue = await PublishAsync(admin, BLUE, initialTag);
        using var green = await PublishAsync(admin, GREEN, blue.Headers.ETag!.Tag);

        // Assert - first publish names "0"; the public read shows the live brand at once (the write evicts its cache)
        initial.StatusCode.Should().Be(HttpStatusCode.OK);
        initialTag.Should().Be("\"0\"");
        blue.StatusCode.Should().Be(HttpStatusCode.OK);
        green.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PublicAccentAsync(anonymous, slug)).Should().Be("#16a34a");

        // Act - roll back to blue, then restore green by number
        using var rolledBack = await PostAsync(admin, RollbackRoute, null, green.Headers.ETag!.Tag);
        (await PublicAccentAsync(anonymous, slug)).Should().Be("#0052d1");
        using var restored = await PostAsync(admin, RestoreRoute, new { number = 2 }, rolledBack.Headers.ETag!.Tag);

        // Assert - every change is a new version; the history names its source
        restored.StatusCode.Should().Be(HttpStatusCode.OK);
        var model = await ReadAsync(restored);
        model.GetProperty("publishedNumber").GetString().Should().Be("4");
        model.GetProperty("published").ValueKind.Should().Be(JsonValueKind.Object);
        (await PublicAccentAsync(anonymous, slug)).Should().Be("#16a34a");

        using var history = await admin.GetAsync(VersionsRoute, Cancellation);
        var items = (await ReadAsync(history)).GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("number").GetString()).Should().Equal("4", "3", "2", "1");
        items[0].GetProperty("source").GetString().Should().Be("restore");
        items[0].GetProperty("restoredFromNumber").GetString().Should().Be("2");
        items[0].GetProperty("createdBy").GetString().Should().Be(world.Tenants[0].Admin.Id.ToString());
    }

    [Fact]
    public async Task A_stale_or_missing_If_Match_is_refused()
    {
        // Arrange
        await using BrandingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, Cancellation);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: Cancellation);
        using var first = await PublishAsync(admin, BLUE, "\"0\"");

        // Act
        using var stale = await PublishAsync(admin, GREEN, "\"0\"");
        using var missing = await PublishAsync(admin, GREEN, null);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        missing.StatusCode.Should().Be((HttpStatusCode)428);
    }

    [Fact]
    public async Task An_invalid_override_lists_its_problems()
    {
        // Arrange
        await using BrandingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, Cancellation);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: Cancellation);

        // Act
        using var response = await PublishAsync(admin, """{"version":1,"extra":true}""", "\"0\"");
        using var unknown = await PostAsync(admin, RestoreRoute, new { number = 9 }, "\"0\"");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = (await ReadAsync(response)).GetProperty("problems")[0];
        problem.GetProperty("path").GetString().Should().Be("/extra");
        problem.GetProperty("code").GetString().Should().Be("unknown_field");
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadAsync(unknown)).GetProperty("problems")[0].GetProperty("code").GetString().Should().Be("unknown_version");
    }

    [Fact]
    public async Task A_creator_reads_the_brand_but_cannot_change_it()
    {
        // Arrange
        await using BrandingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, Cancellation);
        using var creator = await world.AsAsync(TestPersona.Creator, cancellationToken: Cancellation);

        // Act
        using var read = await creator.GetAsync(BrandingRoute, Cancellation);
        using var publish = await PublishAsync(creator, BLUE, "\"0\"");
        using var history = await creator.GetAsync(VersionsRoute, Cancellation);

        // Assert
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        publish.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        history.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_tenant_without_the_entitlement_is_refused_and_reads_no_brand()
    {
        // Arrange - publish while entitled, then lose the entitlement
        IntegrationTestWorld seeded;
        await using (BrandingWebHost entitledHost = new(fixture))
        {
            seeded = await entitledHost.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, Cancellation);
            using var entitledAdmin = await seeded.AsAsync(TestPersona.TenantAdmin, cancellationToken: Cancellation);
            (await PublishAsync(entitledAdmin, BLUE, "\"0\"")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await using BrandingWebHost host = new(fixture, entitled: false);
        var world = On(host, seeded);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: Cancellation);
        using var anonymous = world.AnonymousClient();

        // Act
        using var read = await admin.GetAsync(BrandingRoute, Cancellation);
        using var publish = await PublishAsync(admin, GREEN, read.Headers.ETag!.Tag);
        var publicRead = await PublicTenantAsync(anonymous, await ShortUrlAsync(world));

        // Assert
        var model = await ReadAsync(read);
        model.GetProperty("entitled").GetBoolean().Should().BeFalse();
        model.GetProperty("published").ValueKind.Should().Be(JsonValueKind.Null);
        publish.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        publicRead.GetProperty("branding").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task With_white_labeling_off_the_endpoints_are_not_found_and_the_public_read_has_no_brand()
    {
        // Arrange - publish on a host with the flag on, then read on the shared host, where it is off
        IntegrationTestWorld world;
        await using (BrandingWebHost host = new(fixture))
        {
            world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, Cancellation);
            using var brandingAdmin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: Cancellation);
            (await PublishAsync(brandingAdmin, BLUE, "\"0\"")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var offWorld = On(fixture, world);
        using var admin = await offWorld.AsAsync(TestPersona.TenantAdmin, cancellationToken: Cancellation);
        using var anonymous = offWorld.AnonymousClient();

        // Act
        using var read = await admin.GetAsync(BrandingRoute, Cancellation);
        using var publish = await PublishAsync(admin, GREEN, "\"1\"");
        var publicRead = await PublicTenantAsync(anonymous, await ShortUrlAsync(offWorld));

        // Assert
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        publish.StatusCode.Should().Be(HttpStatusCode.NotFound);
        publicRead.GetProperty("branding").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // The same seeded tenant, users and passwords, reached through another host on the same database.
    private static IntegrationTestWorld On(IIntegrationTestHostFixture host, IntegrationTestWorld world) => new()
    {
        Services = host.Services,
        Seed = host.Seed,
        SeedResult = world.SeedResult,
        CreateClientFactory = host.CreateClient,
        Options = world.Options,
    };

    private static Task<HttpResponseMessage> PublishAsync(HttpClient client, string json, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, PublishRoute)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        AddIfMatch(request, ifMatch);
        return client.SendAsync(request, Cancellation);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Uri route, object? body, string ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body ?? new { }) };
        AddIfMatch(request, ifMatch);
        return client.SendAsync(request, Cancellation);
    }

    private static void AddIfMatch(HttpRequestMessage request, string? ifMatch)
    {
        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(ifMatch));
        }
    }

    private static async Task<string?> PublicAccentAsync(HttpClient anonymous, string slug) =>
        (await PublicTenantAsync(anonymous, slug))
            .GetProperty("branding").GetProperty("tokens").GetProperty("color").GetProperty("primary")
            .GetProperty("$value").GetString();

    private static async Task<JsonElement> PublicTenantAsync(HttpClient anonymous, string slug)
    {
        using var response = await anonymous.GetAsync(new Uri($"/api/public/tenants/{slug}", UriKind.Relative), Cancellation);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadAsync(response);
    }

    private static async Task<string> ShortUrlAsync(IntegrationTestWorld world)
    {
        using var platformAdmin = await world.AsAsync(TestPersona.PlatformAdmin, cancellationToken: Cancellation);
        using var response = await platformAdmin.GetAsync(
            new Uri($"/api/admin/tenants/{world.Tenants[0].Id}", UriKind.Relative),
            Cancellation);
        return (await ReadAsync(response)).GetProperty("shortUrl").GetString()!;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation)).RootElement.Clone();
}
