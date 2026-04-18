extern alias OssWebHost;
extern alias OssIntegrationHost;

using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Endatix.IntegrationTests;

public interface IOssWebApplicationFactory : IAsyncDisposable
{
    IServiceProvider Services { get; }
    HttpClient CreateClient();
    HttpClient CreateClient(WebApplicationFactoryClientOptions options);
}

public sealed class OssWebApplicationFactory : WebApplicationFactory<OssWebHost::Program>, IOssWebApplicationFactory
{
    private readonly string _connectionString;
    private readonly TestDatabaseProvider _provider;

    public OssWebApplicationFactory(string connectionString, TestDatabaseProvider provider)
    {
        _connectionString = connectionString;
        _provider = provider;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        OssWebApplicationFactoryConfiguration.ConfigureCommon(builder, _connectionString, _provider);
    }
}

public sealed class OssDedicatedHostWebApplicationFactory : WebApplicationFactory<OssIntegrationHost::Program>, IOssWebApplicationFactory
{
    private readonly string _connectionString;
    private readonly TestDatabaseProvider _provider;

    public OssDedicatedHostWebApplicationFactory(string connectionString, TestDatabaseProvider provider)
    {
        _connectionString = connectionString;
        _provider = provider;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        OssWebApplicationFactoryConfiguration.ConfigureCommon(builder, _connectionString, _provider);
    }
}

internal static class OssWebApplicationFactoryConfiguration
{
    public static void ConfigureCommon(IWebHostBuilder builder, string connectionString, TestDatabaseProvider provider)
    {
        builder.UseEnvironment(Environments.Staging);

        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("ConnectionStrings:DefaultConnection_DbProvider", provider.ToString());
        builder.UseSetting("Endatix:Data:EnableAutoMigrations", "true");
        builder.UseSetting("Endatix:Data:SeedSampleData", "true");
        builder.UseSetting("Endatix:Data:SeedSampleForms", "true");
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
        builder.UseSetting("Serilog:MinimumLevel:Override:Microsoft", "Warning");
        builder.UseSetting("Serilog:MinimumLevel:Override:System", "Warning");
    }
}
