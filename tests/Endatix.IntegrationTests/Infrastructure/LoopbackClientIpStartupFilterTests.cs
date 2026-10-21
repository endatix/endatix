using System.Net;

namespace Endatix.IntegrationTests.Infrastructure;

/// <summary>
/// Anonymous throttles key on <c>RemoteIpAddress</c>. The test host takes it from <c>X-Forwarded-For</c>
/// when a client sends one, and uses loopback otherwise.
/// </summary>
public sealed class LoopbackClientIpStartupFilterTests
{
    [Fact]
    public void A_forwarded_address_is_the_client_ip()
    {
        var address = LoopbackClientIpStartupFilter.ClientIp("10.1.2.3");

        Assert.Equal(IPAddress.Parse("10.1.2.3"), address);
    }

    [Fact]
    public void The_first_forwarded_address_is_the_client_ip()
    {
        var address = LoopbackClientIpStartupFilter.ClientIp("10.0.0.1, 10.0.0.2");

        Assert.Equal(IPAddress.Parse("10.0.0.1"), address);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-ip")]
    public void Anything_else_stays_on_loopback(string? forwardedFor)
    {
        Assert.Equal(IPAddress.Loopback, LoopbackClientIpStartupFilter.ClientIp(forwardedFor));
    }
}
