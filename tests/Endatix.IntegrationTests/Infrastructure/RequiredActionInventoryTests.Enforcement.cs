using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Endatix.IntegrationTests.RegisteredOperations;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionInventoryTests
{
    [Theory]
    [InlineData("core")]
    [InlineData("jobs")]
    [InlineData("audience")]
    [InlineData("reporting")]
    [InlineData("dashboard-read")]
    [InlineData("dashboard-write")]
    public async Task Every_registered_account_operation_refuses_before_dispatch(string profile)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        await using var factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider);
        foreach (var unavailable in new[] { false, true })
        {
            using var dispatches = new EndpointDispatches();
            await using var host = factory.WithWebHostBuilder(builder =>
            {
                ConfigureProfile(builder, profile);
                builder.ConfigureTestServices(services => ConfigureStanding(services, unavailable));
                builder.ConfigureLogging(logging => logging.AddProvider(dispatches)
                    .AddFilter<EndpointDispatches>(EndpointDispatches.CATEGORY, LogLevel.Information));
            });
            using var owner = host.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
            owner.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;
            var endpoints = host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();
            using var me = await owner.GetAsync("/api/auth/me", ct);
            Assert.Equal(unavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK, me.StatusCode);
            Assert.Equal(1, dispatches.Count);
            var projection = await me.Content.ReadFromJsonAsync<JsonElement>(ct);
            JsonElement? actions = unavailable ? null : projection.GetProperty("requiredActions");
            if (actions.HasValue)
            {
                Assert.Single(actions.Value.EnumerateArray());
            }
            await VerifyOperationsAsync(owner, endpoints, actions);
            Assert.Equal(1, dispatches.Count);
        }
    }

    private static void ConfigureStanding(IServiceCollection services, bool unavailable)
    {
        services.AddScoped(sp => new AccountStanding(sp.GetRequiredService<ICurrentUserAuthorizationService>()));
        services.AddSingleton<IStartupFilter, MatchedRegistration>();
        services.AddSingleton<IRequiredActionProvider>(new MatrixProvider("/resolve"));
        if (unavailable)
        {
            services.AddSingleton<IRequiredActionProvider>(new MatrixProvider("/conflicting-resolution"));
        }
    }

    private static async Task VerifyOperationsAsync(HttpClient owner, IEnumerable<RouteEndpoint> endpoints, JsonElement? actions)
    {
        var ct = TestContext.Current.CancellationToken;
        foreach (var (route, request) in AccountRequests(endpoints))
        {
            using var operation = request;
            using var response = await owner.SendAsync(request, ct);
            response.Headers.GetValues(MatchedRegistration.HEADER).Single().Should().Be(route,
                $"{request.Method} {request.RequestUri!.AbsolutePath} must select {route}");
            response.StatusCode.Should().Be(actions.HasValue ? HttpStatusCode.Forbidden : HttpStatusCode.ServiceUnavailable,
                $"{request.Method} {request.RequestUri!.AbsolutePath} must stop before endpoint dispatch");
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            if (request.Method != HttpMethod.Head)
            {
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                AssertProblem(problem, actions);
            }
        }
    }

    private static void AssertProblem(JsonElement problem, JsonElement? actions)
    {
        if (!actions.HasValue)
        {
            Assert.Contains("Try again", problem.GetProperty("detail").GetString());
            Assert.False(problem.TryGetProperty("requiredActions", out _));
            return;
        }
        Assert.Equal("required_action", problem.GetProperty("errorCode").GetString());
        Assert.Equal("Complete the required actions before continuing.", problem.GetProperty("detail").GetString());
        Assert.True(JsonElement.DeepEquals(actions.Value, problem.GetProperty("requiredActions")));
    }

    private static IEnumerable<(string?, HttpRequestMessage)> AccountRequests(IEnumerable<RouteEndpoint> endpoints) => endpoints
        .Where(route => route.Metadata.GetMetadata<StaticAssetDescriptor>() is null &&
            Access(route.Metadata) is not ("resolver" or "authenticated-resolver" or "platform-resolver" or "open-no-policy" or "anonymous" or "form-token"))
        .SelectMany(route => Methods(route).Select(method => (route.RoutePattern.RawText, ExampleRequest(route, method))));

    private static HttpRequestMessage ExampleRequest(RouteEndpoint route, string method)
    {
        var request = new HttpRequestMessage(new HttpMethod(method == "*" ? "GET" : method), ExamplePath(route));
        if (route.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes is [var mediaType, ..])
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, mediaType);
        }
        return request;
    }

    private sealed class MatchedRegistration : IStartupFilter
    {
        internal const string HEADER = "X-Test-Registration";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers[HEADER] = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
                    return Task.CompletedTask;
                });
                await continuation(context);
            });
            next(app);
        };
    }

    private static string ExamplePath(RouteEndpoint route)
    {
        var path = route.RoutePattern.RawText!;
        foreach (var parameter in route.RoutePattern.Parameters)
        {
            var start = path.IndexOf('{');
            var end = path.IndexOf('}', start);
            path = string.Concat(path.AsSpan(0, start), ExampleValue(parameter.Name, parameter.IsCatchAll), path.AsSpan(end + 1));
        }
        return "/" + path.TrimStart('/');
    }

    private static string ExampleValue(string name, bool catchAll) =>
        catchAll ? "inventory.txt" : name switch
        {
            "schedulerName" => "endatix-jobs",
            _ => "1"
        };

    private sealed class MatrixProvider(string path) : IRequiredActionProvider
    {
        public string CacheConfigurationKey => path;
        public Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RequiredAction>>([new() { Key = "setup", ResolvePath = path, Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Blocking }]);
    }
}
