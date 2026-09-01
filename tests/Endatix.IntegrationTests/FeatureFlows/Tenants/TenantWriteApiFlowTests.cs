using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Common;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests.FeatureFlows.Tenants;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class TenantWriteApiFlowTests
{
    private const string SeedPassword = "Password123!";
    private static readonly Uri TenantsRoute = new("/api/admin/tenants", UriKind.Relative);

    private readonly EndatixIntegrationWebHostFixture _fixture;

    public TenantWriteApiFlowTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_then_patch_tenant_as_platform_admin_keeps_the_generated_short_url()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpClient client = await CreateClientAsync(TestPersona.PlatformAdmin, cancellationToken);

        // Act - create
        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            TenantsRoute,
            new
            {
                name = "  Contoso  ",
                description = "  Second tenant  ",
                allowSelfRegistration = true,
                allowedAuthProviderKeys = new[] { "google", "google" },
                defaultRegistrationRoleName = "Respondent"
            },
            cancellationToken);

        // Assert - create
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement created = await ReadTenantAsync(createResponse, cancellationToken);
        string tenantId = created.GetProperty("id").GetString()!;
        string shortUrl = created.GetProperty("shortUrl").GetString()!;
        ShortUrl.IsValid(shortUrl).Should().BeTrue();
        created.GetProperty("name").GetString().Should().Be("Contoso");
        created.GetProperty("description").GetString().Should().Be("Second tenant");
        created.GetProperty("allowedAuthProviderKeys").EnumerateArray()
            .Select(key => key.GetString())
            .Should().Equal("google");

        // Act - patch the name and the self-registration policy
        using HttpResponseMessage patchResponse = await client.PatchAsJsonAsync(
            TenantUri(tenantId),
            new { name = "Contoso Global", allowSelfRegistration = false },
            cancellationToken);

        // Assert - patch, then re-read to confirm both entities were persisted
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using HttpResponseMessage getResponse = await client.GetAsync(TenantUri(tenantId), cancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement reloaded = await ReadTenantAsync(getResponse, cancellationToken);
        reloaded.GetProperty("name").GetString().Should().Be("Contoso Global");
        reloaded.GetProperty("allowSelfRegistration").GetBoolean().Should().BeFalse();
        reloaded.GetProperty("defaultRegistrationRoleName").GetString().Should().Be("Respondent");
        reloaded.GetProperty("shortUrl").GetString().Should().Be(shortUrl);
    }

    [Fact]
    public async Task Create_tenant_as_tenant_admin_is_forbidden()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpClient client = await CreateClientAsync(TestPersona.TenantAdmin, cancellationToken);

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            TenantsRoute,
            new { name = "Fabrikam" },
            cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> CreateClientAsync(TestPersona persona, CancellationToken cancellationToken)
    {
        IntegrationTestWorld world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            cancellationToken);

        return await world.AsAsync(persona, cancellationToken: cancellationToken);
    }

    private static Uri TenantUri(string tenantId) => new($"/api/admin/tenants/{tenantId}", UriKind.Relative);

    private static async Task<JsonElement> ReadTenantAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
}
