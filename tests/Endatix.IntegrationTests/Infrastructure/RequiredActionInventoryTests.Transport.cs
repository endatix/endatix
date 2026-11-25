using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionInventoryTests
{
    private const string ALLOWED_ORIGIN = "https://workspace.example.test";
    private const string API_HOST = "api.example.test";
    private const string ADMIN_PATH = "/api/admin/auth/settings";

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Transport_preserves_the_challenge_redirect_and_browser_contract(HttpStatusCode standing)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var member = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var dispatches = new EndpointDispatches();
        await using var root = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider);
        await using var host = root.WithWebHostBuilder(builder => ConfigureTransport(builder, standing, dispatches));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await AssertHttpOutcomeAsync(client, null, HttpStatusCode.Unauthorized);
        await AssertHttpOutcomeAsync(client, member.DefaultRequestHeaders.Authorization, HttpStatusCode.Forbidden);
        await AssertHttpOutcomeAsync(client, admin.DefaultRequestHeaders.Authorization, HttpStatusCode.TemporaryRedirect);
        await AssertPreflightAsync(client);
        client.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;
        foreach (var path in new[] { ADMIN_PATH, "/quartz-api/schedulers" })
        {
            using var allowed = await BrowserGetAsync(client, path, ALLOWED_ORIGIN);
            await AssertBrowserResultAsync(allowed, ALLOWED_ORIGIN, standing);
            using var other = await BrowserGetAsync(client, path, "https://other.example.test");
            await AssertBrowserResultAsync(other, null, standing);
        }
        Assert.Equal(standing == HttpStatusCode.OK ? 4 : 0, dispatches.Count);
    }

    private static void ConfigureTransport(IWebHostBuilder builder, HttpStatusCode standing, EndpointDispatches dispatches)
    {
        builder.UseSetting("Endatix:Hosting:UseHttpsRedirection", "true");
        builder.UseSetting("Endatix:Hosting:UseHsts", "true");
        builder.UseSetting("Endatix:BackgroundJobs:Dashboard:Enabled", "true");
        builder.UseSetting("Endatix:Cors:CorsPolicies:0:AllowedOrigins:0", ALLOWED_ORIGIN);
        builder.ConfigureLogging(logging => logging.AddProvider(dispatches)
            .AddFilter<EndpointDispatches>(EndpointDispatches.CATEGORY, LogLevel.Information));
        builder.ConfigureTestServices(services =>
        {
            services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 443);
            if (standing != HttpStatusCode.OK)
            {
                ConfigureStanding(services, standing == HttpStatusCode.ServiceUnavailable);
            }
        });
    }

    private static async Task AssertHttpOutcomeAsync(HttpClient client, AuthenticationHeaderValue? actor, HttpStatusCode expected)
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://{API_HOST}{ADMIN_PATH}");
        request.Headers.Authorization = actor;
        using var response = await client.SendAsync(request, ct);
        Assert.Equal(expected, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(ct));
        if (expected == HttpStatusCode.TemporaryRedirect)
        {
            Assert.Equal($"https://{API_HOST}{ADMIN_PATH}", response.Headers.Location?.AbsoluteUri);
        }
    }

    private static async Task AssertPreflightAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, $"https://{API_HOST}{ADMIN_PATH}");
        request.Headers.Add("Origin", ALLOWED_ORIGIN);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(ALLOWED_ORIGIN, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    private static async Task<HttpResponseMessage> BrowserGetAsync(HttpClient client, string path, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{API_HOST}{path}");
        request.Headers.Add("Origin", origin);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AssertBrowserResultAsync(HttpResponseMessage response, string? allowedOrigin, HttpStatusCode expected)
    {
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(expected, response.StatusCode);
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
        var allowed = response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) ? values.Single() : null;
        Assert.Equal(allowedOrigin, allowed);
        if (expected == HttpStatusCode.OK)
        {
            return;
        }
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (expected == HttpStatusCode.Forbidden)
        {
            Assert.Equal("required_action", problem.GetProperty("errorCode").GetString());
            Assert.Single(problem.GetProperty("requiredActions").EnumerateArray());
        }
        else
        {
            Assert.Contains("Try again", problem.GetProperty("detail").GetString());
        }
    }
}
