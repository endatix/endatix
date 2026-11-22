using System.Net;
using System.Text.Json;

namespace Endatix.IntegrationTests.FeatureFlows.TenantBranding;

/// <summary>Resetting a tenant's branding to the deployment brand (<c>tenant-branding/reset</c>).</summary>
public sealed partial class TenantBrandingFlowTests
{
    [Fact]
    public async Task A_reset_shows_the_deployment_brand_and_a_second_one_changes_nothing()
    {
        // Arrange
        await using BrandingWebHost host = new(fixture);
        var (world, admin) = await TenantAdminAsync(host);
        using var anonymous = world.AnonymousClient();
        var slug = await ShortUrlAsync(world);
        using var nothingToReset = await SendAsync(admin, Post(RESET_ROUTE), "\"0\"");
        using var blue = await SendAsync(admin, Publish(BLUE), "\"0\"");
        var blueAccent = await PublicAccentAsync(anonymous, slug);

        // Act - a reset has no body
        using var reset = await SendAsync(admin, Post(RESET_ROUTE), blue.Headers.ETag!.Tag);
        var afterReset = (await PublicTenantAsync(anonymous, slug)).GetProperty("branding");
        using var resetAgain = await SendAsync(admin, Post(RESET_ROUTE), "\"1\"");

        // Assert - the first reset has nothing to turn off; the second names a stale version and still succeeds
        nothingToReset.Headers.ETag!.Tag.Should().Be("\"0\"");
        blueAccent.Should().Be("#0052d1");
        reset.Headers.ETag!.Tag.Should().Be("\"2\"");
        var resetRead = await ReadAsync(reset);
        resetRead.GetProperty("published").ValueKind.Should().Be(JsonValueKind.Null);
        resetRead.GetProperty("publishedNumber").GetString().Should().Be("2");
        afterReset.ValueKind.Should().Be(JsonValueKind.Null);
        resetAgain.StatusCode.Should().Be(HttpStatusCode.OK);
        resetAgain.Headers.ETag!.Tag.Should().Be("\"2\"");
    }

    [Fact]
    public async Task A_rollback_after_a_reset_brings_the_brand_back_and_the_history_lists_the_reset()
    {
        // Arrange
        await using BrandingWebHost host = new(fixture);
        var (world, admin) = await TenantAdminAsync(host);
        using var anonymous = world.AnonymousClient();
        var slug = await ShortUrlAsync(world);
        using var blue = await SendAsync(admin, Publish(BLUE), "\"0\"");
        using var reset = await SendAsync(admin, Post(RESET_ROUTE), blue.Headers.ETag!.Tag);

        // Act
        using var rolledBack = await SendAsync(admin, Post(ROLLBACK_ROUTE), reset.Headers.ETag!.Tag);
        var rolledBackAccent = await PublicAccentAsync(anonymous, slug);
        using var resetAgain = await SendAsync(admin, Post(RESET_ROUTE), rolledBack.Headers.ETag!.Tag);
        using var green = await SendAsync(admin, Publish(GREEN), resetAgain.Headers.ETag!.Tag);
        using var history = await admin.GetAsync(VERSIONS_ROUTE, Cancellation);

        // Assert
        rolledBackAccent.Should().Be("#0052d1");
        green.Headers.ETag!.Tag.Should().Be("\"5\"");
        (await PublicAccentAsync(anonymous, slug)).Should().Be("#16a34a");
        var items = (await ReadAsync(history)).GetProperty("items").EnumerateArray().ToList();
        items.Select(item => $"{item.GetProperty("number").GetString()}:{item.GetProperty("source").GetString()}")
            .Should().Equal("5:publish", "4:reset", "3:restore", "2:reset", "1:publish");
        items[3].GetProperty("createdBy").GetString().Should().Be(world.Tenants[0].Admin.Id.ToString());
        items[3].GetProperty("createdAt").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Restoring_by_number_brings_a_brand_back_after_a_reset_and_restoring_the_reset_resets_again()
    {
        // Arrange - 1 blue, 2 reset, 3 green
        await using BrandingWebHost host = new(fixture);
        var (world, admin) = await TenantAdminAsync(host);
        using var anonymous = world.AnonymousClient();
        var slug = await ShortUrlAsync(world);
        using var blue = await SendAsync(admin, Publish(BLUE), "\"0\"");
        using var reset = await SendAsync(admin, Post(RESET_ROUTE), blue.Headers.ETag!.Tag);
        using var green = await SendAsync(admin, Publish(GREEN), reset.Headers.ETag!.Tag);

        // Act
        using var restoredReset = await SendAsync(admin, Post(RESTORE_ROUTE, new { number = 2 }), green.Headers.ETag!.Tag);
        var afterRestoredReset = (await PublicTenantAsync(anonymous, slug)).GetProperty("branding");
        using var restoredBlue = await SendAsync(admin, Post(RESTORE_ROUTE, new { number = 1 }), restoredReset.Headers.ETag!.Tag);

        // Assert
        var restoredResetRead = await ReadAsync(restoredReset);
        restoredResetRead.GetProperty("published").ValueKind.Should().Be(JsonValueKind.Null);
        restoredResetRead.GetProperty("publishedNumber").GetString().Should().Be("4");
        afterRestoredReset.ValueKind.Should().Be(JsonValueKind.Null);
        restoredBlue.Headers.ETag!.Tag.Should().Be("\"5\"");
        (await PublicAccentAsync(anonymous, slug)).Should().Be("#0052d1");
        using var history = await admin.GetAsync(VERSIONS_ROUTE, Cancellation);
        var items = (await ReadAsync(history)).GetProperty("items").EnumerateArray().ToList();
        items.Select(item => $"{item.GetProperty("number").GetString()}:{item.GetProperty("source").GetString()}<{item.GetProperty("restoredFromNumber").GetString()}")
            .Should().Equal("5:restore<1", "4:reset<2", "3:publish<", "2:reset<", "1:publish<");
    }
}
