using Endatix.Framework.Configuration;
using Endatix.Framework.Hosting;
using Endatix.Hosting.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Hosting.Tests.Options;

public class ReverseProxyServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEndatixReverseProxy_WhenReverseProxyIsDisabled_LeavesForwardedHeadersDisabled()
    {
        using var provider = CreateServiceProvider(new HostingOptions(), isDevelopment: true);

        var options = provider
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.None);
        options.KnownIPNetworks.Should().NotBeEmpty();
        options.KnownProxies.Should().NotBeEmpty();
    }

    [Fact]
    public void AddEndatixReverseProxy_WhenReverseProxyIsEnabled_ConfiguresForwardedHeaders()
    {
        using var provider = CreateServiceProvider(
            new HostingOptions
            {
                ReverseProxy = new ReverseProxyOptions
                {
                    Enabled = true,
                    TrustAllProxiesInDevelopment = false
                }
            },
            isDevelopment: false);

        var options = provider
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;

        options.ForwardedHeaders.Should().Be(
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedHost |
            ForwardedHeaders.XForwardedProto |
            ForwardedHeaders.XForwardedPrefix);
    }

    [Fact]
    public void AddEndatixReverseProxy_WhenDevelopmentTrustAllIsEnabled_ClearsKnownProxyRestrictions()
    {
        using var provider = CreateServiceProvider(
            new HostingOptions
            {
                ReverseProxy = new ReverseProxyOptions
                {
                    Enabled = true,
                    TrustAllProxiesInDevelopment = true
                }
            },
            isDevelopment: true);

        var options = provider
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;

        options.KnownIPNetworks.Should().BeEmpty();
        options.KnownProxies.Should().BeEmpty();
    }

    [Fact]
    public void AddEndatixReverseProxy_WhenProductionTrustAllIsEnabled_KeepsKnownProxyRestrictions()
    {
        using var provider = CreateServiceProvider(
            new HostingOptions
            {
                ReverseProxy = new ReverseProxyOptions
                {
                    Enabled = true,
                    TrustAllProxiesInDevelopment = true
                }
            },
            isDevelopment: false);

        var options = provider
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;

        options.KnownIPNetworks.Should().NotBeEmpty();
        options.KnownProxies.Should().NotBeEmpty();
    }

    private static ServiceProvider CreateServiceProvider(HostingOptions hostingOptions, bool isDevelopment)
    {
        ServiceCollection services = new();

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(hostingOptions));
        services.AddSingleton<IAppEnvironment>(new TestAppEnvironment(isDevelopment));
        services.AddEndatixReverseProxy();

        return services.BuildServiceProvider();
    }

    private sealed class TestAppEnvironment(bool isDevelopment) : IAppEnvironment
    {
        public string EnvironmentName => isDevelopment ? "Development" : "Production";

        public bool IsDevelopment() => isDevelopment;
    }
}
