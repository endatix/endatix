extern alias EndatixWebHost;

using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience;
using Endatix.Modules.Audience.Persistence;
using Endatix.Outbox.Engine;
using System.Globalization;
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
    private readonly OutboxPolling _outboxPolling;

    public EndatixWebApplicationFactory(string connectionString, TestDatabaseProvider provider)
        : this(connectionString, provider, OutboxPolling.Fast)
    {
    }

    internal EndatixWebApplicationFactory(string connectionString, TestDatabaseProvider provider, OutboxPolling outboxPolling)
    {
        _connectionString = connectionString;
        _provider = provider;
        _outboxPolling = outboxPolling;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        EndatixWebApplicationFactoryConfiguration.ConfigureCommon(builder, _connectionString, _provider, _outboxPolling);
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

/// <summary>How a test host's outbox relay waits between polls.</summary>
internal enum OutboxPolling
{
    /// <summary>Every <see cref="EndatixWebApplicationFactoryConfiguration.OutboxPollInterval"/>, set in code through the engine's test seam.</summary>
    Fast,

    /// <summary>Every second through <c>Endatix:Outbox:PollIntervalSeconds</c>, the relay's production timer path.</summary>
    Configured,
}

internal static class EndatixWebApplicationFactoryConfiguration
{
    /// <summary>
    /// The relay's wait between polls on a fast-polling test host. A test that switches the relay on sees it claim within
    /// this rather than the production second; with the switch off a poll only asks the switch, so it costs no query.
    /// </summary>
    public static readonly TimeSpan OutboxPollInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Rows per chunk of a staged Audience import on every test host, so a few hundred rows cross several chunks. It
    /// does not divide the 500-row write batch, so batches start inside a chunk, as they do in production.
    /// </summary>
    public const int IMPORT_CHUNK_ROWS = 150;

    public static void ConfigureCommon(
        IWebHostBuilder builder,
        string connectionString,
        TestDatabaseProvider provider,
        OutboxPolling outboxPolling)
    {
        builder.UseEnvironment(Environments.Staging);

        builder.ConfigureServices(AddTestServices);
        builder.ConfigureTestServices(BackgroundWorkRegistration.AddSwitchedOff);
        builder.ConfigureTestServices(HostStopOutcome.Register);

        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("ConnectionStrings:DefaultConnection_DbProvider", provider.ToString());
        builder.UseSetting("Endatix:Data:EnableAutoMigrations", "true");
        builder.UseSetting("Endatix:Data:SeedSampleData", "false");
        builder.UseSetting("Endatix:Data:SeedSampleForms", "false");

        ConfigureFeatureFlags(builder);

        ConfigureOutboxPolling(builder, outboxPolling);

        builder.UseSetting(
            $"{AudienceOptions.SECTION_NAME}:{nameof(AudienceOptions.ImportChunkRows)}",
            IMPORT_CHUNK_ROWS.ToString(CultureInfo.InvariantCulture));

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

    private static void ConfigureOutboxPolling(IWebHostBuilder builder, OutboxPolling outboxPolling)
    {
        if (outboxPolling == OutboxPolling.Configured)
        {
            builder.UseSetting("Endatix:Outbox:PollIntervalSeconds", "1");
            return;
        }

        builder.ConfigureTestServices(services => services.UseOutboxPollInterval(OutboxPollInterval));
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
