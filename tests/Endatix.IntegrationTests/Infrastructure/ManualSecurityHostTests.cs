using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Endatix.IntegrationTests;

[Trait("Category", "Infrastructure")]
public sealed class ManualSecurityHostTests
{
    [Fact]
    public async Task Plain_framework_security_needs_no_standing_registration()
    {
        await using var host = await StartAsync();
        using var client = host.GetTestClient();
        var ct = TestContext.Current.CancellationToken;

        using var challenge = await client.GetAsync("/account", ct);
        client.DefaultRequestHeaders.Authorization = Credential(host, "member");
        using var denied = await client.GetAsync("/account", ct);
        client.DefaultRequestHeaders.Authorization = Credential(host, "owner");
        using var admitted = await client.GetAsync("/account", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, admitted.StatusCode);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Forbidden)]
    [InlineData(true, HttpStatusCode.ServiceUnavailable)]
    public async Task Explicit_standing_refuses_the_authorized_account(bool unavailable, HttpStatusCode expected)
    {
        var snapshot = AuthorizationData.ForAuthenticatedUser("owner", 1, [], []) with
        {
            RequiredActionsUnavailable = unavailable,
            RequiredActions = [new() { Key = "profile", ResolvePath = "/profile", Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Blocking }]
        };
        var projection = Substitute.For<ICurrentUserAuthorizationService>();
        projection.GetAuthorizationDataAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(snapshot));
        await using var host = await StartAsync(services => services.AddScoped(_ => new AccountStanding(projection)));
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = Credential(host, "owner");

        using var response = await client.GetAsync("/account", TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        if (unavailable)
        {
            Assert.Contains("Try again", problem.GetProperty("detail").GetString());
        }
        else
        {
            Assert.Equal("required_action", problem.GetProperty("errorCode").GetString());
            Assert.Equal("profile", Assert.Single(problem.GetProperty("requiredActions").EnumerateArray()).GetProperty("key").GetString());
        }
    }

    [Fact]
    public async Task A_broken_standing_registration_propagates_its_resolution_failure()
    {
        await using var host = await StartAsync(services => services.AddScoped(sp =>
            new AccountStanding(sp.GetRequiredService<ICurrentUserAuthorizationService>())));
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = Credential(host, "owner");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("/account", TestContext.Current.CancellationToken));

        Assert.Contains(nameof(ICurrentUserAuthorizationService), error.Message);
    }

    private static async Task<WebApplication> StartAsync(Action<IServiceCollection>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken();
        builder.Services.AddAuthorization();
        configure?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseRouting();
        app.UseEndatix(security => security.UseSecurity());
        app.MapGet("/account", () => Results.NoContent()).RequireAuthorization(policy => policy.RequireRole("owner"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static AuthenticationHeaderValue Credential(WebApplication host, string role)
    {
        const string scheme = BearerTokenDefaults.AuthenticationScheme;
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], scheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) }, scheme);
        var options = host.Services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(scheme);
        return new AuthenticationHeaderValue("Bearer", options.BearerTokenProtector.Protect(ticket));
    }
}
