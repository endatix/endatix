using System.Net;
using System.Net.Sockets;
using Endatix.Core.Common;
using Endatix.Infrastructure.Networking;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Networking;

public class PublicAddressConnectorTests
{
    private static readonly OutboundAddressPolicy PublicOnly = OutboundAddressPolicy.PublicOnly;
    private static readonly OutboundAddressPolicy AnyAddress = new(AllowPrivateNetworkTargets: true);

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("[::1]")]
    [InlineData("::ffff:169.254.10.20")]
    public async Task ConnectAsync_NonPublicLiteral_RefusesWithoutResolving(string host)
    {
        // Arrange
        var resolver = new FakeResolver();
        var connector = new PublicAddressConnector(PublicOnly, resolver.ResolveAsync);

        // Act
        var connect = () => connector.ConnectAsync(new DnsEndPoint(host, 80), CancellationToken.None).AsTask();

        // Assert
        (await connect.Should().ThrowAsync<OutboundAddressRefusedException>())
            .Which.Reason.Should().Be(OutboundAddressRefusalReason.NonPublicAddress);
        resolver.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("metadata.google.internal")]
    [InlineData("api.acme.svc.cluster.local")]
    public async Task ConnectAsync_ReservedName_RefusesBeforeResolving(string host)
    {
        // Arrange
        var resolver = new FakeResolver();
        var connector = new PublicAddressConnector(PublicOnly, resolver.ResolveAsync);

        // Act
        var connect = () => connector.ConnectAsync(new DnsEndPoint(host, 80), CancellationToken.None).AsTask();

        // Assert
        (await connect.Should().ThrowAsync<OutboundAddressRefusedException>())
            .Which.Reason.Should().Be(OutboundAddressRefusalReason.ReservedName);
        resolver.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ConnectAsync_AnyResolvedAddressNotPublic_Refuses()
    {
        // Arrange
        var resolver = new FakeResolver(IPAddress.Parse("8.8.8.8"), IPAddress.Parse("10.0.0.1"));
        var connector = new PublicAddressConnector(PublicOnly, resolver.ResolveAsync);

        // Act
        var connect = () => connector.ConnectAsync(new DnsEndPoint("hooks.acme.test", 80), CancellationToken.None).AsTask();

        // Assert
        (await connect.Should().ThrowAsync<OutboundAddressRefusedException>())
            .Which.Reason.Should().Be(OutboundAddressRefusalReason.NonPublicAddress);
        resolver.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ConnectAsync_ResolverFails_PropagatesTheFailureAsIs()
    {
        // Arrange
        var connector = new PublicAddressConnector(
            PublicOnly, (_, _) => throw new SocketException((int)SocketError.HostNotFound));

        // Act
        var connect = () => connector.ConnectAsync(new DnsEndPoint("hooks.acme.test", 80), CancellationToken.None).AsTask();

        // Assert
        (await connect.Should().ThrowAsync<SocketException>())
            .Which.SocketErrorCode.Should().Be(SocketError.HostNotFound);
    }

    [Fact]
    public async Task ConnectAsync_ResolverFindsNoAddress_FailsAsHostNotFound()
    {
        // Arrange
        var connector = new PublicAddressConnector(PublicOnly, new FakeResolver().ResolveAsync);

        // Act
        var connect = () => connector.ConnectAsync(new DnsEndPoint("hooks.acme.test", 80), CancellationToken.None).AsTask();

        // Assert
        (await connect.Should().ThrowAsync<SocketException>())
            .Which.SocketErrorCode.Should().Be(SocketError.HostNotFound);
    }

    [Fact]
    public async Task ConnectAsync_PrivateTargetsAllowed_ConnectsToTheResolvedLoopbackAddress()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var resolver = new FakeResolver(IPAddress.Loopback);
        var connector = new PublicAddressConnector(AnyAddress, resolver.ResolveAsync);

        // Act
        await using var stream = await connector.ConnectAsync(new DnsEndPoint("sink.acme.test", port), CancellationToken.None);
        using var accepted = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);

        // Assert
        accepted.Connected.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_PrivateTargetsAllowed_TriesTheNextAddressWhenOneRefuses()
    {
        // Arrange: nothing listens on the first address's port, so its connect fails and the second is tried.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var resolver = new FakeResolver(IPAddress.IPv6Loopback, IPAddress.Loopback);
        var connector = new PublicAddressConnector(AnyAddress, resolver.ResolveAsync);

        // Act
        await using var stream = await connector.ConnectAsync(new DnsEndPoint("sink.acme.test", port), CancellationToken.None);
        using var accepted = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);

        // Assert
        ((IPEndPoint)accepted.Client.RemoteEndPoint!).Address.Should().Be(IPAddress.Loopback);
    }

    [Fact]
    public async Task ConnectAsync_Cancelled_StopsWithoutConnectingOrTryingTheNextAddress()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var connector = new PublicAddressConnector(AnyAddress, new FakeResolver(IPAddress.Loopback, IPAddress.Loopback).ResolveAsync);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var connect = () => connector.ConnectAsync(new DnsEndPoint("sink.acme.test", port), cancellation.Token).AsTask();

        // Assert
        await connect.Should().ThrowAsync<OperationCanceledException>();
        listener.Pending().Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_EveryAddressFails_ThrowsTheLastFailure()
    {
        // Arrange: a port that was free a moment ago; nothing listens on it now.
        int port;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
        }

        var connector = new PublicAddressConnector(AnyAddress, new FakeResolver(IPAddress.Loopback).ResolveAsync);

        // Act
        var connect = () => connector.ConnectAsync(new DnsEndPoint("sink.acme.test", port), CancellationToken.None).AsTask();

        // Assert
        (await connect.Should().ThrowAsync<SocketException>())
            .Which.SocketErrorCode.Should().Be(SocketError.ConnectionRefused);
    }

    [Fact]
    public async Task ConnectAsync_PrivateTargetsAllowed_ConnectsToAnIPv4MappedAddressAsIPv4()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var connector = new PublicAddressConnector(AnyAddress, new FakeResolver(IPAddress.Loopback.MapToIPv6()).ResolveAsync);

        // Act
        await using var stream = await connector.ConnectAsync(new DnsEndPoint("sink.acme.test", port), CancellationToken.None);
        using var accepted = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);

        // Assert
        accepted.Connected.Should().BeTrue();
    }

    [Fact]
    public void IsCauseOf_RefusalWrappedByTheHandler_ReturnsTrue()
    {
        // Arrange
        var refusal = new OutboundAddressRefusedException(OutboundAddressRefusalReason.NonPublicAddress);
        var wrapped = new HttpRequestException("A connection could not be made.", refusal);

        // Act
        var isCause = OutboundAddressRefusedException.IsCauseOf(wrapped);

        // Assert
        isCause.Should().BeTrue();
    }

    [Fact]
    public void IsCauseOf_OtherFailure_ReturnsFalse()
    {
        // Arrange
        var failure = new HttpRequestException("A connection could not be made.", new SocketException((int)SocketError.HostNotFound));

        // Act
        var isCause = OutboundAddressRefusedException.IsCauseOf(failure);

        // Assert
        isCause.Should().BeFalse();
    }

    [Fact]
    public void IsCauseOf_NoException_ReturnsFalse()
    {
        // Act
        var isCause = OutboundAddressRefusedException.IsCauseOf(null);

        // Assert
        isCause.Should().BeFalse();
    }

    private sealed class FakeResolver(params IPAddress[] addresses)
    {
        public int Calls { get; private set; }

        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(addresses);
        }
    }
}
