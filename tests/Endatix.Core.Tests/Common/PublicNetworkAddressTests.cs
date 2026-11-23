using System.Net;
using Endatix.Core.Common;

namespace Endatix.Core.Tests.Common;

public class PublicNetworkAddressTests
{
    // Each range at its first and last address.
    public static TheoryData<string> NonPublicAddresses() =>
    [
        // IPv4
        "0.0.0.0", "0.255.255.255",
        "10.0.0.0", "10.255.255.255",
        "100.64.0.0", "100.127.255.255",
        "127.0.0.0", "127.0.0.1", "127.255.255.255",
        "169.254.0.0", "169.254.10.20", "169.254.255.255",
        "172.16.0.0", "172.31.255.255",
        "192.0.0.0", "192.0.0.255",
        "192.0.2.0", "192.0.2.255",
        "192.168.0.0", "192.168.255.255",
        "198.18.0.0", "198.19.255.255",
        "198.51.100.0", "198.51.100.255",
        "203.0.113.0", "203.0.113.255",
        "224.0.0.0", "239.255.255.255",
        "240.0.0.0", "255.255.255.255",

        // IPv6
        "::", "::1",
        "fc00::", "fdff:ffff:ffff:ffff:ffff:ffff:ffff:ffff",
        "fe80::", "febf:ffff:ffff:ffff:ffff:ffff:ffff:ffff",
        "fec0::", "feff:ffff:ffff:ffff:ffff:ffff:ffff:ffff",
        "ff00::", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff",
        "2001:db8::", "2001:db8:ffff:ffff:ffff:ffff:ffff:ffff",
        "100::", "100::ffff:ffff:ffff:ffff",

        // Prefixes that carry an IPv4 address, refused whole
        "::0.0.0.2", "::255.255.255.255",
        "64:ff9b::", "64:ff9b::ffff:ffff",
        "64:ff9b:1::", "64:ff9b:1:ffff:ffff:ffff:ffff:ffff",
        "2002::", "2002:ffff:ffff:ffff:ffff:ffff:ffff:ffff",
        "2001::", "2001:0:ffff:ffff:ffff:ffff:ffff:ffff",
        "::ffff:0:0.0.0.0", "::ffff:0:255.255.255.255",

        // IPv4-mapped, judged as the IPv4 address it carries
        "::ffff:127.0.0.1", "::ffff:169.254.10.20", "::ffff:10.0.0.1", "::ffff:0.0.0.0",
    ];

    // The neighbour just outside each range, and public controls.
    public static TheoryData<string> PublicAddresses() =>
    [
        // IPv4
        "1.0.0.0",
        "9.255.255.255", "11.0.0.0",
        "100.63.255.255", "100.128.0.0",
        "126.255.255.255", "128.0.0.0",
        "169.253.255.255", "169.255.0.0",
        "172.15.255.255", "172.32.0.0",
        "191.255.255.255", "192.0.1.0",
        "192.0.3.0",
        "192.167.255.255", "192.169.0.0",
        "198.17.255.255", "198.20.0.0",
        "198.51.99.255", "198.51.101.0",
        "203.0.112.255", "203.0.114.0",
        "223.255.255.255",
        "8.8.8.8", "1.1.1.1",

        // IPv6
        "fbff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", "fe00::", "fe7f:ffff:ffff:ffff:ffff:ffff:ffff:ffff",
        "2001:db7:ffff:ffff:ffff:ffff:ffff:ffff", "2001:db9::",
        "100:0:0:1::",
        "64:ff9b::1:0:0", "64:ff9b:2::",
        "2003::", "2001:1::",
        "2606:4700:4700::1111", "2001:4860:4860::8888",

        // IPv4-mapped public
        "::ffff:8.8.8.8",
    ];

    // A public IPv4 address inside a prefix that is refused whole: refused, not unwrapped.
    public static TheoryData<string> WrappedPublicAddresses() =>
    [
        "::8.8.8.8",                                  // IPv4-compatible
        "64:ff9b::808:808",                           // NAT64
        "64:ff9b:1::808:808",                         // local-use NAT64
        "2002:808:808::",                             // 6to4
        "2001:0:4136:e378:8000:63bf:f7f7:f7f7",       // Teredo, client 8.8.8.8
        "::ffff:0:8.8.8.8",                           // IPv4-translated
    ];

    [Theory]
    [MemberData(nameof(NonPublicAddresses))]
    public void IsPublic_AddressInANonPublicRange_ReturnsFalse(string address)
    {
        // Act
        var isPublic = PublicNetworkAddress.IsPublic(IPAddress.Parse(address));

        // Assert
        isPublic.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(PublicAddresses))]
    public void IsPublic_AddressOutsideEveryRange_ReturnsTrue(string address)
    {
        // Act
        var isPublic = PublicNetworkAddress.IsPublic(IPAddress.Parse(address));

        // Assert
        isPublic.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(WrappedPublicAddresses))]
    public void IsPublic_PublicIPv4InsideARefusedPrefix_ReturnsFalse(string address)
    {
        // Act
        var isPublic = PublicNetworkAddress.IsPublic(IPAddress.Parse(address));

        // Assert
        isPublic.Should().BeFalse();
    }

    [Fact]
    public void IsPublic_PublicIPv6WithAScope_ReturnsFalse()
    {
        // Arrange
        var scoped = new IPAddress(IPAddress.Parse("2606:4700:4700::1111").GetAddressBytes(), scopeid: 3);

        // Act
        var isPublic = PublicNetworkAddress.IsPublic(scoped);

        // Assert
        isPublic.Should().BeFalse();
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData("localhost.")]
    [InlineData("app.localhost")]
    [InlineData("db.internal")]
    [InlineData("metadata.google.internal")]
    [InlineData("Metadata.Google.Internal.")]
    [InlineData("printer.local")]
    [InlineData("api.acme.svc.cluster.local")]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.1.")]
    [InlineData("10.0.0.5")]
    [InlineData("[::1]")]
    [InlineData("::1")]
    [InlineData("fe80::1%eth0")]
    [InlineData("[fe80::1%25eth0]")]
    [InlineData("[::ffff:169.254.10.20]")]
    public void IsKnownPrivateHost_PrivateLiteralOrReservedName_ReturnsTrue(string host)
    {
        // Act
        var isPrivate = PublicNetworkAddress.IsKnownPrivateHost(host);

        // Assert
        isPrivate.Should().BeTrue();
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("hooks.example.com")]
    [InlineData("internal.example.com")]
    [InlineData("localhost.example.com")]
    [InlineData("mylocal")]
    [InlineData("notlocalhost")]
    [InlineData("8.8.8.8")]
    [InlineData("[2606:4700:4700::1111]")]
    public void IsKnownPrivateHost_PublicLiteralOrOtherName_ReturnsFalse(string host)
    {
        // Act
        var isPrivate = PublicNetworkAddress.IsKnownPrivateHost(host);

        // Assert
        isPrivate.Should().BeFalse();
    }
}
