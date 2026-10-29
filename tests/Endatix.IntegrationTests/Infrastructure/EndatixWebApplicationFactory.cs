extern alias EndatixWebHost;

using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Persistence;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Endatix.IntegrationTests;

public interface IEndatixWebApplicationFactory : IAsyncDisposable
{
    IServiceProvider Services { get; }
    HttpClient CreateClient();
    HttpClient CreateClient(WebApplicationFactoryClientOptions options);
}

public sealed class EndatixWebApplicationFactory : WebApplicationFactory<EndatixWebHost::Program>, IEndatixWebApplicationFactory
{
    private readonly string _connectionString;
    private readonly TestDatabaseProvider _provider;

    public EndatixWebApplicationFactory(string connectionString, TestDatabaseProvider provider)
    {
        _connectionString = connectionString;
        _provider = provider;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        EndatixWebApplicationFactoryConfiguration.ConfigureCommon(builder, _connectionString, _provider);
    }

    // Factories derived through WithWebHostBuilder build their hosts through this method too.
    protected override IHost CreateHost(IHostBuilder builder) => new StopOnceHost(base.CreateHost(builder));
}

/// <summary>
/// TestServer leaves <c>RemoteIpAddress</c> empty, and anonymous throttles key on it.
/// A client that sends <c>X-Forwarded-For</c> is that address; every other request stays on loopback.
/// The host's forwarded-headers middleware does not do this here: it trusts only <c>::1</c>, and the
/// test peer is not a proxy.
/// </summary>
internal sealed class LoopbackClientIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (HttpContext context, RequestDelegate continuation) =>
        {
            var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
            context.Connection.RemoteIpAddress ??= ClientIp(string.IsNullOrEmpty(forwarded) ? null : forwarded);
            await continuation(context);
        });
        next(app);
    };

    /// <summary>The first address in <paramref name="forwardedFor"/>, or loopback when it is missing or not an address.</summary>
    internal static IPAddress ClientIp(string? forwardedFor)
    {
        var first = forwardedFor?.Split(',', 2)[0].Trim();
        return IPAddress.TryParse(first, out var address) ? address : IPAddress.Loopback;
    }
}

internal static class EndatixWebApplicationFactoryConfiguration
{
    public static void ConfigureCommon(IWebHostBuilder builder, string connectionString, TestDatabaseProvider provider)
    {
        builder.UseEnvironment(Environments.Staging);

        builder.ConfigureServices(AddTestServices);
        builder.ConfigureTestServices(BackgroundWorkRegistration.AddSwitchedOff);

        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("ConnectionStrings:DefaultConnection_DbProvider", provider.ToString());
        builder.UseSetting("Endatix:Data:EnableAutoMigrations", "true");
        builder.UseSetting("Endatix:Data:SeedSampleData", "false");
        builder.UseSetting("Endatix:Data:SeedSampleForms", "false");

        ConfigureFeatureFlags(builder);

        // A test that switches the outbox relay on sees it claim within a second rather than five.
        builder.UseSetting("Endatix:Outbox:PollIntervalSeconds", "1");

        var auth = IntegrationTestAuthSettings.FromEnvironment();
        builder.UseSetting("Endatix:Auth:Providers:Keycloak:Enabled", "false");
        builder.UseSetting("Endatix:Auth:Providers:EndatixJwt:Enabled", "true");
        builder.UseSetting("Endatix:Auth:Providers:EndatixJwt:SigningKey", auth.SigningKey);
        builder.UseSetting("Endatix:Auth:Providers:EndatixJwt:Issuer", auth.Issuer);
        builder.UseSetting("Endatix:Auth:Providers:EndatixJwt:Audiences:0", auth.Audience);
        builder.UseSetting("Endatix:Auth:Providers:EndatixJwt:RequireHttpsMetadata", "false");

        builder.UseSetting("Endatix:Submissions:AccessTokenSigningKey", auth.SigningKey);

        // Keeps the integration-test console quiet. These replace the former Serilog:MinimumLevel
        // keys one-for-one; leaving those in place would have gone unnoticed, since an unread
        // configuration section fails silently rather than erroring.
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft", "Warning");
        builder.UseSetting("Logging:LogLevel:System", "Warning");
    }

    private static void AddTestServices(IServiceCollection services)
    {
        // Test-only throwing route; see UnhandledExceptionRouteStartupFilter.
        services.AddSingleton<IStartupFilter, UnhandledExceptionRouteStartupFilter>();
        // TestServer has no peer address. FastEndpoints throttle forbids the call when it
        // cannot tell clients apart, so give the host a loopback address.
        services.AddSingleton<IStartupFilter, LoopbackClientIpStartupFilter>();
        // Inert until a test arms it; lets a test pause one request at a chosen statement.
        services.AddSingleton<AppCommandGate>();
        services.ConfigureDbContext<AppDbContext>((provider, options) =>
            options.AddInterceptors(provider.GetRequiredService<AppCommandGate>()));
        services.ConfigureDbContext<AudiencePostgreSqlDbContext>((provider, options) =>
            options.AddInterceptors(provider.GetRequiredService<AppCommandGate>()));
        services.ConfigureDbContext<AudienceSqlServerDbContext>((provider, options) =>
            options.AddInterceptors(provider.GetRequiredService<AppCommandGate>()));
    }

    private static void ConfigureFeatureFlags(IWebHostBuilder builder)
    {
        // Deployment-scoped and off by default, so the tenant management endpoints would 404 here.
        builder.UseSetting("Endatix:FeatureFlags:MultiTenancy", "true");

        builder.UseSetting("Endatix:FeatureFlags:JobsModule", "true");
        builder.UseSetting("Endatix:FeatureFlags:PersonalizationModule", "true");
        // ReportingModule stays off: a test that calls Reporting endpoints opts in with ReportingWebHost.
    }
}
