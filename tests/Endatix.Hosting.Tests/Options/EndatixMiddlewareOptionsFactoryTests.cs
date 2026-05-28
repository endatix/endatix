using Endatix.Api.Builders;
using Endatix.Framework.Configuration;
using Endatix.Hosting.Options;

namespace Endatix.Hosting.Tests.Options;

public class EndatixMiddlewareOptionsFactoryTests
{
    [Fact]
    public void Create_WhenReverseProxyIsDisabled_KeepsHttpsAndHstsEnabled()
    {
        var options = EndatixMiddlewareOptionsFactory.Create(new HostingOptions());

        options.UseForwardedHeaders.Should().BeFalse();
        options.UseHsts.Should().BeTrue();
        options.UseHttpsRedirection.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenReverseProxyIsEnabled_DisablesAppLevelHttpsAndHstsByDefault()
    {
        var options = new HostingOptions
        {
            ReverseProxy = new ReverseProxyOptions
            {
                Enabled = true
            }
        };

        var middlewareOptions = EndatixMiddlewareOptionsFactory.Create(options);

        middlewareOptions.UseForwardedHeaders.Should().BeTrue();
        middlewareOptions.UseHsts.Should().BeFalse();
        middlewareOptions.UseHttpsRedirection.Should().BeFalse();
    }

    [Fact]
    public void Create_WhenReverseProxyIsEnabled_RespectsExplicitHttpsAndHstsOverrides()
    {
        var options = new HostingOptions
        {
            UseHsts = true,
            UseHttpsRedirection = true,
            ReverseProxy = new ReverseProxyOptions
            {
                Enabled = true
            }
        };

        var middlewareOptions = EndatixMiddlewareOptionsFactory.Create(options);

        middlewareOptions.UseForwardedHeaders.Should().BeTrue();
        middlewareOptions.UseHsts.Should().BeTrue();
        middlewareOptions.UseHttpsRedirection.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenApiOptionsAreProvided_PreservesApiOptions()
    {
        ApiOptions apiOptions = new()
        {
            UseSwagger = false,
            SwaggerPath = "/docs"
        };

        var middlewareOptions = EndatixMiddlewareOptionsFactory.Create(new HostingOptions(), apiOptions);

        middlewareOptions.ApiOptions.Should().BeSameAs(apiOptions);
        middlewareOptions.ApiOptions.UseSwagger.Should().BeFalse();
        middlewareOptions.ApiOptions.SwaggerPath.Should().Be("/docs");
    }
}
