extern alias EndatixWebHost;

using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
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

/// <summary>TestServer leaves <c>RemoteIpAddress</c> empty. Anonymous throttles key on it.</summary>
internal sealed class LoopbackClientIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (HttpContext context, RequestDelegate continuation) =>
        {
            context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
            await continuation(context);
        });
        next(app);
    };
}

internal static class EndatixWebApplicationFactoryConfiguration
{
    public static void ConfigureCommon(IWebHostBuilder builder, string connectionString, TestDatabaseProvider provider)
    {
        builder.UseEnvironment(Environments.Staging);

        // Test-only throwing route; see UnhandledExceptionRouteStartupFilter.
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, UnhandledExceptionRouteStartupFilter>();
            // TestServer has no peer address. FastEndpoints throttle forbids the call when it
            // cannot tell clients apart, so give the host a loopback address.
            services.AddSingleton<IStartupFilter, LoopbackClientIpStartupFilter>();
        });

        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("ConnectionStrings:DefaultConnection_DbProvider", provider.ToString());
        builder.UseSetting("Endatix:Data:EnableAutoMigrations", "true");
        builder.UseSetting("Endatix:Data:SeedSampleData", "false");
        builder.UseSetting("Endatix:Data:SeedSampleForms", "false");

        ConfigureFeatureFlags(builder, provider);

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

    private static void ConfigureFeatureFlags(IWebHostBuilder builder, TestDatabaseProvider provider)
    {
        // Deployment-scoped and off by default, so the tenant management endpoints would 404 here.
        builder.UseSetting("Endatix:FeatureFlags:MultiTenancy", "true");

        // Jobs is PostgreSQL-only and fails startup deliberately on anything else, so its flag
        // follows the provider this run is using. Tests that need it skip on SQL Server.
        string postgreSqlOnly = (provider == TestDatabaseProvider.PostgreSql).ToString();
        builder.UseSetting("Endatix:FeatureFlags:JobsModule", postgreSqlOnly);
        builder.UseSetting("Endatix:FeatureFlags:PersonalizationModule", "true");
        builder.UseSetting("Endatix:FeatureFlags:ReportingModule", "true");
    }
}
