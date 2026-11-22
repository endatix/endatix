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

    private const string BrandingRoute = "/api/tenant-branding";
    private const string PublishRoute = "/api/tenant-branding/published";
    private const string RestoreRoute = "/api/tenant-branding/restore";
    private const string RollbackRoute = "/api/tenant-branding/rollback";
    private const string VersionsRoute = "/api/tenant-branding/versions";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_admin_publishes_and_the_public_read_shows_the_live_brand_at_once()
    {
        // Arrange
        await using BrandingWebHost host = new(fixture);
        var (world, admin) = await TenantAdminAsync(host);
        using var anonymous = world.AnonymousClient();
        var slug = await ShortUrlAsync(world);
        using var initial = await admin.GetAsync(BrandingRoute, Cancellation);

        // Act
        using var blue = await SendAsync(admin, Publish(BLUE), initial.Headers.ETag!.Tag);
        var blueAccent = await PublicAccentAsync(anonymous, slug);
        using var green = await SendAsync(admin, Publish(GREEN), blue.Headers.ETag!.Tag);

        // Assert - the first publish names "0"; each write evicts the cached public read
        initial.Headers.ETag.Tag.Should().Be("\"0\"");
        blue.StatusCode.Should().Be(HttpStatusCode.OK);
        blueAccent.Should().Be("#0052d1");
        green.Headers.ETag!.Tag.Should().Be("\"2\"");
        (await PublicAccentAsync(anonymous, slug)).Should().Be("#16a34a");
    }

    [Fact]
    public async Task Rollback_and_restore_append_versions_the_history_lists()
    {
        // Arrange
        await using BrandingWebHost host = new(fixture);
        var (world, admin) = await TenantAdminAsync(host);
        using var anonymous = world.AnonymousClient();
        using var blue = await SendAsync(admin, Publish(BLUE), "\"0\"");
        using var green = await SendAsync(admin, Publish(GREEN), blue.Headers.ETag!.Tag);

        // Act - a rollback has no body
        using var rolledBack = await SendAsync(admin, Post(RollbackRoute), green.Headers.ETag!.Tag);
        var rolledBackAccent = await PublicAccentAsync(anonymous, await ShortUrlAsync(world));
        using var restored = await SendAsync(admin, Post(RestoreRoute, new { number = 2 }), rolledBack.Headers.ETag!.Tag);
        using var history = await admin.GetAsync(VersionsRoute, Cancellation);

        // Assert
        rolledBack.StatusCode.Should().Be(HttpStatusCode.OK);
        rolledBackAccent.Should().Be("#0052d1");
        restored.Headers.ETag!.Tag.Should().Be("\"4\"");
        (await ReadAsync(restored)).GetProperty("published").ValueKind.Should().Be(JsonValueKind.Object);
        var items = (await ReadAsync(history)).GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("number").GetString()).Should().Equal("4", "3", "2", "1");
        items[0].GetProperty("source").GetString().Should().Be("restore");
        items[0].GetProperty("restoredFromNumber").GetString().Should().Be("2");
        items[0].GetProperty("createdBy").GetString().Should().Be(world.Tenants[0].Admin.Id.ToString());
    }

    [Fact]
    public async Task A_stale_If_Match_is_refused_with_the_current_version_on_every_write()
    {
        // Arrange
        await using BrandingWebHost host = new(fixture);
        var (_, admin) = await TenantAdminAsync(host);
        using var blue = await SendAsync(admin, Publish(BLUE), "\"0\"");
        using var green = await SendAsync(admin, Publish(GREEN), blue.Headers.ETag!.Tag);

        // Act
        using var publish = await SendAsync(admin, Publish(BLUE), "\"1\"");
        using var restore = await SendAsync(admin, Post(RestoreRoute, new { number = 1 }), "\"1\"");
        using var rollback = await SendAsync(admin, Post(RollbackRoute), "\"1\"");
        using var missing = await SendAsync(admin, Publish(BLUE), null);

        // Assert
        foreach (var stale in new[] { publish, restore, rollback })
        {
            stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
            stale.Headers.ETag!.Tag.Should().Be("\"2\"");
            (await ReadAsync(stale)).GetProperty("version").GetString().Should().Be("2");
        }

        missing.StatusCode.Should().Be((HttpStatusCode)428);
    }

    [Theory]
    [InlineData("""{"version":1,"extra":true}""", "/extra", "unknown_field")]
    [InlineData("""{"version":1""", "", "invalid_json")]
    [InlineData("", "", "invalid_json")]
    public async Task An_unusable_override_lists_its_problems(string body, string path, string code)
    {
        // Arrange
        await using BrandingWebHost host = new(fixture);
        var (_, admin) = await TenantAdminAsync(host);

        // Act
        using var response = await SendAsync(admin, Publish(body), "\"0\"");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = (await ReadAsync(response)).GetProperty("problems")[0];
        problem.GetProperty("path").GetString().Should().Be(path);
        problem.GetProperty("code").GetString().Should().Be(code);
    }

    [Fact]
    public async Task Restore_and_rollback_name_what_they_cannot_find()
    {
        // Arrange
        await using BrandingWebHost host = new(fixture);
        var (_, admin) = await TenantAdminAsync(host);
        using var blue = await SendAsync(admin, Publish(BLUE), "\"0\"");

        // Act
        using var unknown = await SendAsync(admin, Post(RestoreRoute, new { number = 9 }), "\"1\"");
        using var noPrevious = await SendAsync(admin, Post(RollbackRoute), "\"1\"");

        // Assert
        (await ProblemCodeAsync(unknown)).Should().Be("unknown_version");
        (await ProblemCodeAsync(noPrevious)).Should().Be("no_previous_version");
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
        using var publish = await SendAsync(creator, Publish(BLUE), "\"0\"");
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
        var seeded = await PublishBlueAsync();
        await using BrandingWebHost host = new(fixture, entitled: false);
        var world = On(host, seeded);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: Cancellation);
        using var anonymous = world.AnonymousClient();

        // Act
        using var read = await admin.GetAsync(BrandingRoute, Cancellation);
        using var publish = await SendAsync(admin, Publish(GREEN), read.Headers.ETag!.Tag);
        using var history = await admin.GetAsync(VersionsRoute, Cancellation);
        var publicRead = await PublicTenantAsync(anonymous, await ShortUrlAsync(world));

        // Assert
        var model = await ReadAsync(read);
        model.GetProperty("entitled").GetBoolean().Should().BeFalse();
        model.GetProperty("published").ValueKind.Should().Be(JsonValueKind.Null);
        publish.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadAsync(history)).GetProperty("items").GetArrayLength().Should().Be(0);
        publicRead.GetProperty("branding").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task With_white_labeling_off_every_route_is_not_found_and_the_public_read_has_no_brand()
    {
        // Arrange - publish on a host with the flag on, then use the shared host, where it is off
        var world = On(fixture, await PublishBlueAsync());
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: Cancellation);
        using var anonymous = world.AnonymousClient();

        // Act
        HttpRequestMessage[] requests =
        [
            new(HttpMethod.Get, BrandingRoute), new(HttpMethod.Get, VersionsRoute), Publish(GREEN),
            Post(RestoreRoute, new { number = 1 }), Post(RollbackRoute),
        ];
        var statuses = new List<HttpStatusCode>();
        foreach (var request in requests)
        {
            using var response = await SendAsync(admin, request, "\"1\"");
            statuses.Add(response.StatusCode);
        }

        // Assert
        statuses.Should().AllBeEquivalentTo(HttpStatusCode.NotFound);
        (await PublicTenantAsync(anonymous, await ShortUrlAsync(world))).GetProperty("branding").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_signed_in_reader_from_another_tenant_reads_and_caches_the_brand_of_the_slug()
    {
        // Arrange - tenant A publishes; nothing has read A's public tenant yet, so the cache is cold
        await using BrandingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword }, Cancellation);
        using var adminA = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 0, cancellationToken: Cancellation);
        using var adminB = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 1, cancellationToken: Cancellation);
        using var anonymous = world.AnonymousClient();
        var slugA = await ShortUrlAsync(world, tenantIndex: 0);
        using var published = await SendAsync(adminA, Publish(BLUE), "\"0\"");

        // Act - B's token on A's slug fills the cache that the anonymous reader then hits
        var signedInAccent = await PublicAccentAsync(adminB, slugA);
        var anonymousAccent = await PublicAccentAsync(anonymous, slugA);

        // Assert
        published.StatusCode.Should().Be(HttpStatusCode.OK);
        signedInAccent.Should().Be("#0052d1");
        anonymousAccent.Should().Be("#0052d1");
    }

    [Fact]
    public async Task Each_tenant_reads_and_writes_only_its_own_branding()
    {
        // Arrange - A has two versions, B one
        await using BrandingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword }, Cancellation);
        using var adminA = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 0, cancellationToken: Cancellation);
        using var adminB = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 1, cancellationToken: Cancellation);
        using var anonymous = world.AnonymousClient();
        using var blueA = await SendAsync(adminA, Publish(BLUE), "\"0\"");
        using var greenA = await SendAsync(adminA, Publish(GREEN), blueA.Headers.ETag!.Tag);
        var slugB = await ShortUrlAsync(world, tenantIndex: 1);
        var emptyB = await PublicTenantAsync(anonymous, slugB);

        // Act
        using var readB = await adminB.GetAsync(BrandingRoute, Cancellation);
        using var historyB = await adminB.GetAsync(VersionsRoute, Cancellation);
        using var restoreB = await SendAsync(adminB, Post(RestoreRoute, new { number = 2 }), "\"0\"");
        using var blueB = await SendAsync(adminB, Publish(BLUE), "\"0\"");
        var publicB = await PublicAccentAsync(anonymous, slugB);
        using var blueAgainA = await SendAsync(adminA, Publish(BLUE), greenA.Headers.ETag!.Tag);
        var publicBAfterA = await PublicAccentAsync(anonymous, slugB);

        // Assert - B starts at "0" with no history; A's numbers do not exist for B; A's writes leave B's brand
        greenA.Headers.ETag!.Tag.Should().Be("\"2\"");
        emptyB.GetProperty("branding").ValueKind.Should().Be(JsonValueKind.Null);
        readB.Headers.ETag!.Tag.Should().Be("\"0\"");
        (await ReadAsync(readB)).GetProperty("published").ValueKind.Should().Be(JsonValueKind.Null);
        (await ReadAsync(historyB)).GetProperty("items").GetArrayLength().Should().Be(0);
        (await ProblemCodeAsync(restoreB)).Should().Be("unknown_version");
        blueB.Headers.ETag!.Tag.Should().Be("\"1\"");
        publicB.Should().Be("#0052d1");
        blueAgainA.Headers.ETag!.Tag.Should().Be("\"3\"");
        publicBAfterA.Should().Be("#0052d1");
    }

    // Seeds a tenant on a host with the flag on and the entitlement, and publishes BLUE as version 1.
    private async Task<IntegrationTestWorld> PublishBlueAsync()
    {
        await using BrandingWebHost host = new(fixture);
        var (world, admin) = await TenantAdminAsync(host);
        using (admin)
        {
            using var published = await SendAsync(admin, Publish(BLUE), "\"0\"");
            published.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        return world;
    }

    private static async Task<(IntegrationTestWorld World, HttpClient Admin)> TenantAdminAsync(IIntegrationTestHostFixture host)
    {
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, Cancellation);
        return (world, await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: Cancellation));
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

    private static HttpRequestMessage Publish(string json) =>
        new(HttpMethod.Put, PublishRoute) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpRequestMessage Post(string route, object? body = null) =>
        new(HttpMethod.Post, route) { Content = body is null ? null : JsonContent.Create(body) };

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request, string? ifMatch)
    {
        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(ifMatch));
        }

        return client.SendAsync(request, Cancellation);
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        return (await ReadAsync(response)).GetProperty("problems")[0].GetProperty("code").GetString();
    }

    private static async Task<string?> PublicAccentAsync(HttpClient client, string slug) =>
        (await PublicTenantAsync(client, slug))
            .GetProperty("branding").GetProperty("tokens").GetProperty("color").GetProperty("primary")
            .GetProperty("$value").GetString();

    private static async Task<JsonElement> PublicTenantAsync(HttpClient client, string slug)
    {
        using var response = await client.GetAsync($"/api/public/tenants/{slug}", Cancellation);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadAsync(response);
    }

    private static async Task<string> ShortUrlAsync(IntegrationTestWorld world, int tenantIndex = 0)
    {
        using var platformAdmin = await world.AsAsync(TestPersona.PlatformAdmin, cancellationToken: Cancellation);
        using var response = await platformAdmin.GetAsync($"/api/admin/tenants/{world.Tenants[tenantIndex].Id}", Cancellation);
        return (await ReadAsync(response)).GetProperty("shortUrl").GetString()!;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation)).RootElement.Clone();
}
