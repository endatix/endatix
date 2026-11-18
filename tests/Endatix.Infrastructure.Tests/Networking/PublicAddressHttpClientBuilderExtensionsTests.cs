using System.Net;
using System.Net.Sockets;
using System.Text;
using Endatix.Core.Common;
using Endatix.Infrastructure.Features.BackgroundJobs.Handlers;
using Endatix.Infrastructure.Features.WebHooks;
using Endatix.Infrastructure.Networking;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Endatix.Infrastructure.Tests.Networking;

public class PublicAddressHttpClientBuilderExtensionsTests
{
    private const string TypedWebHookClient = nameof(WebHookServer);

    public static TheoryData<string> WebHookClientNames() => [TypedWebHookClient, WebHookDeliveryJobHandler.HttpClientName];

    [Theory]
    [MemberData(nameof(WebHookClientNames))]
    public void AddWebHookProcessing_WebHookClient_EndsInAGuardedSocketsHandler(string clientName)
    {
        // Arrange
        using var provider = WebHookProcessing(new Dictionary<string, string?>());

        // Act
        var primary = PrimaryHandlerOf(provider, clientName);

        // Assert
        primary.Should().BeOfType<SocketsHttpHandler>().Which.Should().Match<SocketsHttpHandler>(handler =>
            handler.ConnectCallback != null && !handler.AllowAutoRedirect && !handler.UseProxy);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void AddWebHookProcessing_Switch_IsTheRegisteredPolicy(string? configured, bool allowed)
    {
        // Arrange
        using var provider = WebHookProcessing(new Dictionary<string, string?>
        {
            ["Endatix:WebHooks:ServerSettings:AllowPrivateNetworkTargets"] = configured,
        });

        // Act
        var policy = provider.GetRequiredService<OutboundAddressPolicy>();

        // Assert
        policy.AllowPrivateNetworkTargets.Should().Be(allowed);
    }

    [Fact]
    public void AddWebHookProcessing_AfterAnotherGuardedClient_StillAppliesTheSwitch()
    {
        // Arrange — a guarded client registered first adds the public-only default.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Endatix:WebHooks:ServerSettings:AllowPrivateNetworkTargets"] = "true" })
            .Build());
        services.AddHttpClient("acme").ConnectToPublicAddressesOnly();

        // Act
        services.AddWebHookProcessing();
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<OutboundAddressPolicy>().AllowPrivateNetworkTargets.Should().BeTrue();
    }

    [Fact]
    public void ConnectToPublicAddressesOnly_LaterPrimaryHandlerConfiguration_IsOverridden()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHttpClient<WebHookServer>()
            .ConnectToPublicAddressesOnly()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = true, UseProxy = true });
        using var provider = services.BuildServiceProvider();

        // Act
        var primary = PrimaryHandlerOf(provider, TypedWebHookClient);

        // Assert
        primary.Should().BeOfType<SocketsHttpHandler>().Which.Should().Match<SocketsHttpHandler>(handler =>
            handler.ConnectCallback != null && !handler.AllowAutoRedirect && !handler.UseProxy);
    }

    [Fact]
    public void ConnectToPublicAddressesOnly_PrimaryHandlerIsNotSockets_FailsTheClient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHttpClient("acme")
            .ConnectToPublicAddressesOnly()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler());
        using var provider = services.BuildServiceProvider();

        // Act
        var create = () => provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("acme");

        // Assert
        create.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ConnectToPublicAddressesOnly_OtherClient_IsLeftAsIs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHttpClient("guarded").ConnectToPublicAddressesOnly();
        services.AddHttpClient("other").ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler());
        using var provider = services.BuildServiceProvider();

        // Act
        var primary = PrimaryHandlerOf(provider, "other");

        // Assert
        primary.Should().BeOfType<SocketsHttpHandler>().Which.ConnectCallback.Should().BeNull();
    }

    [Fact]
    public async Task ConnectToPublicAddressesOnly_LoopbackEndpoint_IsRefusedAndNothingConnects()
    {
        // Arrange
        await using var sink = new TcpSink(IPAddress.Loopback);
        using var provider = Guarded(OutboundAddressPolicy.PublicOnly);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("guarded");

        // Act
        var send = () => client.GetAsync($"http://127.0.0.1:{sink.Port}/", TestContext.Current.CancellationToken);

        // Assert
        (await send.Should().ThrowAsync<HttpRequestException>())
            .Which.Should().Match<HttpRequestException>(ex => OutboundAddressRefusedException.IsCauseOf(ex));
        sink.Connections.Should().Be(0);
    }

    [Fact]
    public async Task ConnectToPublicAddressesOnly_NameThatResolvesToAPrivateAddress_IsRefusedAndNothingConnects()
    {
        // Arrange — a name that is not reserved, so only the resolved address can refuse it.
        await using var sink = new TcpSink(IPAddress.Loopback);
        using var provider = Guarded(OutboundAddressPolicy.PublicOnly, ResolvesTo(IPAddress.Loopback));
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("guarded");

        // Act
        var send = () => client.GetAsync($"http://hooks.acme.test:{sink.Port}/", TestContext.Current.CancellationToken);

        // Assert
        (await send.Should().ThrowAsync<HttpRequestException>())
            .Which.Should().Match<HttpRequestException>(ex =>
                OutboundAddressRefusedException.Of(ex)!.Reason == OutboundAddressRefusalReason.NonPublicAddress);
        sink.Connections.Should().Be(0);
    }

    [Fact]
    public async Task ConnectToPublicAddressesOnly_PrivateTargetsAllowed_ConnectsToTheResolvedAddress()
    {
        // Arrange
        await using var sink = new TcpSink(IPAddress.Loopback);
        using var provider = Guarded(new OutboundAddressPolicy(AllowPrivateNetworkTargets: true), ResolvesTo(IPAddress.Loopback));
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("guarded");

        // Act
        using var response = await client.GetAsync($"http://hooks.acme.test:{sink.Port}/", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        sink.Connections.Should().Be(1);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("[::1]")]
    public async Task ConnectToPublicAddressesOnly_PrivateTargetsAllowed_ReachesTheLoopbackEndpoint(string host)
    {
        // Arrange
        Assert.SkipUnless(host == "127.0.0.1" || Socket.OSSupportsIPv6, "This machine has no IPv6 loopback.");
        await using var sink = new TcpSink(host == "127.0.0.1" ? IPAddress.Loopback : IPAddress.IPv6Loopback);
        using var provider = Guarded(new OutboundAddressPolicy(AllowPrivateNetworkTargets: true));
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("guarded");

        // Act
        using var response = await client.GetAsync($"http://{host}:{sink.Port}/", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        sink.Connections.Should().Be(1);
    }

    private static ServiceProvider WebHookProcessing(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddWebHookProcessing();
        return services.BuildServiceProvider();
    }

    private static ServiceProvider Guarded(
        OutboundAddressPolicy policy,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(policy);
        services.AddHttpClient("guarded").ConnectToPublicAddressesOnly();
        if (resolve is not null)
        {
            services.Configure<PublicAddressGuardOptions>("guarded", guard => guard.Resolve = resolve);
        }

        return services.BuildServiceProvider();
    }

    private static Func<string, CancellationToken, Task<IPAddress[]>> ResolvesTo(IPAddress address) =>
        (_, _) => Task.FromResult(new[] { address });

    private static HttpMessageHandler PrimaryHandlerOf(IServiceProvider provider, string clientName)
    {
        HttpMessageHandler handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(clientName);
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }

        return handler;
    }

    /// <summary>Accepts connections on a loopback port and answers each request with 204.</summary>
    private sealed class TcpSink : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _serving;
        private int _connections;

        public TcpSink(IPAddress address)
        {
            _listener = new TcpListener(address, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serving = ServeAsync();
        }

        public int Port { get; }

        public int Connections => Volatile.Read(ref _connections);

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            try
            {
                await _serving;
            }
            catch (OperationCanceledException)
            {
                // Stopping cancels the pending accept.
            }

            _stop.Dispose();
        }

        private async Task ServeAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                Interlocked.Increment(ref _connections);
                var stream = client.GetStream();
                await ReadRequestHeadAsync(stream);
                await stream.WriteAsync(
                    Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"),
                    _stop.Token);
            }
        }

        private async Task ReadRequestHeadAsync(NetworkStream stream)
        {
            var buffer = new byte[4096];
            var request = new StringBuilder();
            while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                var read = await stream.ReadAsync(buffer, _stop.Token);
                if (read == 0)
                {
                    return;
                }

                request.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }
        }
    }
}
