using System.Text.Json;
using Endatix.Core.Common;
using Endatix.Core.Entities;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

public class WebHookConfigurationEndpointUrlTests
{
    private const string PrivateMessage = "A webhook endpoint URL points to a private or local network address. Use a public address.";
    private const string UnsupportedMessage = "A webhook endpoint URL must be an absolute http or https URL.";
    private static readonly OutboundAddressPolicy AnyAddress = new(AllowPrivateNetworkTargets: true);

    [Theory]
    [InlineData("http://127.0.0.1:8080/hooks")]
    [InlineData("http://169.254.10.20/hooks")]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://192.168.1.10/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://[::ffff:169.254.10.20]/")]
    [InlineData("http://localhost/admin")]
    [InlineData("https://db.internal/")]
    [InlineData("http://LOCALHOST./")]
    [InlineData("http://2130706433/")]
    [InlineData("http://0x7f.1/")]
    [InlineData("http://0177.0.0.1/")]
    [InlineData("http://127.1/")]
    [InlineData("http://acme@127.0.0.1/")]
    [InlineData("https://user:pass@10.0.0.5/hooks")]
    public void EndpointUrlError_PrivateHost_ReturnsThePrivateMessage(string url)
    {
        // Act
        var error = WebHookConfiguration.EndpointUrlError(Settings(url), OutboundAddressPolicy.PublicOnly);

        // Assert
        error.Should().Be(PrivateMessage);
    }

    [Theory]
    [InlineData("/relative/path")]
    [InlineData("hooks.example.com/endatix")]
    [InlineData("not a url")]
    [InlineData("ftp://files.example.com/drop")]
    [InlineData("file:///var/hooks")]
    [InlineData("mailto:hooks@example.com")]
    public void EndpointUrlError_NotAbsoluteHttpOrHttps_ReturnsTheUnsupportedMessage(string url)
    {
        // Act
        var error = WebHookConfiguration.EndpointUrlError(Settings(url), OutboundAddressPolicy.PublicOnly);

        // Assert
        error.Should().Be(UnsupportedMessage);
    }

    [Theory]
    [InlineData("ftp://10.0.0.5/drop")]
    [InlineData("/relative/path")]
    public void EndpointUrlError_PrivateTargetsAllowed_StillRefusesAnUnsupportedUrl(string url)
    {
        // Act
        var error = WebHookConfiguration.EndpointUrlError(Settings(url), AnyAddress);

        // Assert
        error.Should().Be(UnsupportedMessage);
    }

    [Theory]
    [InlineData("http://127.0.0.1:8080/hooks")]
    [InlineData("http://localhost/admin")]
    public void EndpointUrlError_PrivateTargetsAllowed_AcceptsAPrivateHost(string url)
    {
        // Act
        var error = WebHookConfiguration.EndpointUrlError(Settings(url), AnyAddress);

        // Assert
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("https://hooks.example.com/endatix")]
    [InlineData("http://8.8.8.8/")]
    [InlineData("https://internal.example.com/")]
    [InlineData("https://u:p@example.com/")]
    [InlineData("HTTPS://hooks.example.com/")]
    public void EndpointUrlError_PublicHttpOrHttpsUrl_ReturnsNull(string url)
    {
        // Act
        var error = WebHookConfiguration.EndpointUrlError(Settings(url), OutboundAddressPolicy.PublicOnly);

        // Assert
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"Events":{}}""")]
    [InlineData("""{"Events":{"SubmissionCompleted":{"WebHookEndpoints":[{"Url":""}]}}}""")]
    public void EndpointUrlError_NothingToJudge_ReturnsNull(string? json)
    {
        // Act
        var error = WebHookConfiguration.EndpointUrlError(json, OutboundAddressPolicy.PublicOnly);

        // Assert
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("""{"events":{"SubmissionCompleted":{"isEnabled":true,"webHookEndpoints":[{"url":"http://127.0.0.1/"}]}}}""")]
    [InlineData("""{"Events":{"SubmissionCompleted":{"WebHookEndpoints":[{"Url":"http://127.0.0.1/","url":"https://hooks.example.com/"}]}}}""")]
    [InlineData("""{"Events":{"SubmissionCompleted":{"WebHookEndpoints":[{"url":"https://hooks.example.com/","Url":"http://127.0.0.1/"}]}}}""")]
    [InlineData("""{"Events":{"A":{"WebHookEndpoints":[{"Url":"https://hooks.example.com/"}]},"B":{"WebHookEndpoints":[{"Url":"http://10.1.2.3/"}]}}}""")]
    public void EndpointUrlError_PrivateUrlUnderAnyCasingOrPosition_ReturnsThePrivateMessage(string json)
    {
        // Act
        var error = WebHookConfiguration.EndpointUrlError(json, OutboundAddressPolicy.PublicOnly);

        // Assert
        error.Should().Be(PrivateMessage);
    }

    [Fact]
    public void EndpointUrlError_JsonElement_JudgesLikeTheString()
    {
        // Arrange
        using var document = JsonDocument.Parse(Settings("ftp://files.example.com/"));

        // Act
        var error = WebHookConfiguration.EndpointUrlError(document.RootElement, OutboundAddressPolicy.PublicOnly);

        // Assert
        error.Should().Be(UnsupportedMessage);
    }

    private static string Settings(string url) =>
        JsonSerializer.Serialize(new WebHookConfiguration
        {
            Events =
            {
                ["SubmissionCompleted"] = new WebHookEventConfig
                {
                    IsEnabled = true,
                    WebHookEndpoints = [new WebHookEndpointConfig { Url = url }],
                },
            },
        });
}
