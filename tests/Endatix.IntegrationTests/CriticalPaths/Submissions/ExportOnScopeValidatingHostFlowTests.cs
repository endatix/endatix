extern alias EndatixWebHost;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The export request validator on a host that validates scopes, as every Development host does (#1712). FastEndpoints
/// builds the validator once from the root provider, and its format rule resolves the scoped exporter factory per
/// request, so both a supported and an unsupported format reach the rule.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P1")]
public sealed class ExportOnScopeValidatingHostFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";

    [Fact]
    public async Task Export_with_a_supported_format_on_a_scope_validating_host_succeeds()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using ScopeValidatingHost host = new(fixture);
        using var client = await TenantAdminAsync(host, cancellationToken);
        var formId = await CreateEnabledFormAsync(client, cancellationToken);

        // Act
        using var response = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions/export",
            new { exportFormat = "csv" },
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Export_with_an_unsupported_format_on_a_scope_validating_host_is_rejected()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using ScopeValidatingHost host = new(fixture);
        using var client = await TenantAdminAsync(host, cancellationToken);
        var formId = await CreateEnabledFormAsync(client, cancellationToken);

        // Act
        using var response = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions/export",
            new { exportFormat = "pdf" },
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Export format not supported. Supported formats:", await response.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
    }

    private static async Task<HttpClient> TenantAdminAsync(ScopeValidatingHost host, CancellationToken cancellationToken)
    {
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            cancellationToken);
        return await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
    }

    private static async Task<string> CreateEnabledFormAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"scope-validating-export-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q1"}]}]}""",
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("id").GetString()!;
    }

    /// <summary>A second web host on the collection's database that validates scopes, as a Development host does.</summary>
    private sealed class ScopeValidatingHost : IIntegrationTestHostFixture, IAsyncDisposable
    {
        private readonly WebApplicationFactory<EndatixWebHost::Program> _factory;

        public ScopeValidatingHost(EndatixIntegrationWebHostFixture fixture)
        {
            Database = fixture.Database;
            _factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider)
                .WithWebHostBuilder(builder =>
                    builder.UseDefaultServiceProvider(options => options.ValidateScopes = true));
            Seed = new IntegrationSeedBuilder(_factory.Services);
        }

        public DatabaseInfrastructureFixture Database { get; }

        public DatabaseCheckpoint Checkpoint => Database.Checkpoint;

        public IServiceProvider Services => _factory.Services;

        public IntegrationSeedBuilder Seed { get; }

        public HttpClient CreateClient() => _factory.CreateClient();

        public ValueTask DisposeAsync() => _factory.DisposeAsync();
    }
}
