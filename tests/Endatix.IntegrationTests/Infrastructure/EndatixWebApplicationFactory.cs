extern alias EndatixWebHost;

using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Hosting;
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
}

internal static class EndatixWebApplicationFactoryConfiguration
{
    public static void ConfigureCommon(IWebHostBuilder builder, string connectionString, TestDatabaseProvider provider)
    {
        builder.UseEnvironment(Environments.Staging);

        // Test-only throwing route; see UnhandledExceptionRouteStartupFilter.
        builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter, UnhandledExceptionRouteStartupFilter>());

        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("ConnectionStrings:DefaultConnection_DbProvider", provider.ToString());
        builder.UseSetting("Endatix:Data:EnableAutoMigrations", "true");
        builder.UseSetting("Endatix:Data:SeedSampleData", "false");
        builder.UseSetting("Endatix:Data:SeedSampleForms", "false");

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
}
