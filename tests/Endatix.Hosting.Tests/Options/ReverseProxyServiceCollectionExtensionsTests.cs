using System.Net;
using Endatix.Framework.Configuration;
using Endatix.Framework.Hosting;
using Endatix.Hosting.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

    [Fact]
    public void AddEndatixReverseProxy_WhenKnownNetworksAreSet_AddsThemAndRaisesForwardLimit()
    {
        using var provider = CreateServiceProvider(
            new HostingOptions
            {
                ReverseProxy = new ReverseProxyOptions
                {
                    Enabled = true,
                    TrustAllProxiesInDevelopment = false,
                    KnownProxies = ["10.1.2.3"],
                    KnownNetworks = ["10.0.0.0/8"]
                }
            },
            isDevelopment: false);

        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.KnownProxies.Should().Contain(IPAddress.Parse("10.1.2.3"));
        options.KnownIPNetworks.Should().Contain(network => network.ToString() == "10.0.0.0/8");
        options.ForwardLimit.Should().Be(ReverseProxyServiceCollectionExtensions.ForwardLimit);
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    [InlineData("::0/0")]
    [InlineData("0.0.0.0/00")]
    [InlineData("1.2.3.4/0")]
    public void AddEndatixReverseProxy_WhenNetworkIsEveryone_Throws(string everyone)
    {
        var act = () => CreateServiceProvider(
            new HostingOptions
            {
                ReverseProxy = new ReverseProxyOptions
                {
                    Enabled = true,
                    TrustAllProxiesInDevelopment = false,
                    KnownNetworks = [everyone]
                }
            },
            isDevelopment: false).GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Normalize_ReplacesForwardedForWithTheConnectionAddress()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.8");
        context.Request.Headers["X-Forwarded-For"] = "1.2.3.4, 203.0.113.8";
        context.Request.Headers["X-Azure-ClientIP"] = "1.2.3.4";
        context.Request.Headers["True-Client-IP"] = "1.2.3.4";

        ClientIpHeaderMiddleware.Normalize(context);

        context.Request.Headers["X-Forwarded-For"].ToString().Should().Be("203.0.113.8");
        context.Request.Headers.ContainsKey("X-Azure-ClientIP").Should().BeFalse();
        context.Request.Headers.ContainsKey("True-Client-IP").Should().BeFalse();
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
