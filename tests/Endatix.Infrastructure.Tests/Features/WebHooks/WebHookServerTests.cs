using System.Net;
using System.Text;
using System.Text.Json;
using Endatix.Core.Features.WebHooks;
using Endatix.Framework.Logging;
using Endatix.Infrastructure.Features.WebHooks;
using Endatix.Infrastructure.Networking;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.WebHooks;

public class WebHookServerTests
{
    private readonly ILogger<WebHookServer> _logger;

    public WebHookServerTests()
    {
        _logger = Substitute.For<ILogger<WebHookServer>>();
    }

    [Fact]
    public async Task FireWebHookAsync_WithValidRequest_ReturnsDelivered()
    {
        // Arrange
        var handler = new MockHttpHandler(HttpStatusCode.OK, "Success");
        var httpClient = new HttpClient(handler);
        var server = new WebHookServer(httpClient, _logger);

        var message = CreateTestWebHookMessage();
        var instructions = new TaskInstructions("https://api.example.com/webhooks");

        // Act
        var result = await server.FireWebHookAsync(message, instructions, CancellationToken.None);

        // Assert
        result.Should().Be(WebHookDeliveryOutcome.Delivered);
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest!.RequestUri.Should().Be("https://api.example.com/webhooks");
    }

    [Fact]
    public async Task FireWebHookAsync_WithInvalidUri_ReturnsFailed()
    {
        // Arrange
        var handler = new MockHttpHandler(HttpStatusCode.OK, "Success");
        var httpClient = new HttpClient(handler);
        var server = new WebHookServer(httpClient, _logger);

        var message = CreateTestWebHookMessage();
        var instructions = new TaskInstructions("not-a-valid-uri");

        // Act
        var result = await server.FireWebHookAsync(message, instructions, CancellationToken.None);

        // Assert
        result.Should().Be(WebHookDeliveryOutcome.Failed);
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task FireWebHookAsync_WithNullUri_ReturnsFailed()
    {
        // Arrange
        var handler = new MockHttpHandler(HttpStatusCode.OK, "Success");
        var httpClient = new HttpClient(handler);
        var server = new WebHookServer(httpClient, _logger);

        var message = CreateTestWebHookMessage();
        var instructions = new TaskInstructions(null!);

        // Act
        var result = await server.FireWebHookAsync(message, instructions, CancellationToken.None);

        // Assert
        result.Should().Be(WebHookDeliveryOutcome.Failed);
        handler.LastRequest.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task FireWebHookAsync_WithErrorStatusCode_ReturnsFailed(HttpStatusCode statusCode)
    {
        // Arrange
        var handler = new MockHttpHandler(statusCode, "Error");
        var httpClient = new HttpClient(handler);
        var server = new WebHookServer(httpClient, _logger);

        var message = CreateTestWebHookMessage();
        var instructions = new TaskInstructions("https://api.example.com/webhooks");

        // Act
        var result = await server.FireWebHookAsync(message, instructions, CancellationToken.None);

        // Assert
        result.Should().Be(WebHookDeliveryOutcome.Failed);
    }

    [Fact]
    public async Task FireWebHookAsync_WithHttpException_ReturnsFailed()
    {
        // Arrange
        var handler = new ThrowingHttpHandler();
        var httpClient = new HttpClient(handler);
        var server = new WebHookServer(httpClient, _logger);

        var message = CreateTestWebHookMessage();
        var instructions = new TaskInstructions("https://api.example.com/webhooks");

        // Act
        var result = await server.FireWebHookAsync(message, instructions, CancellationToken.None);

        // Assert
        result.Should().Be(WebHookDeliveryOutcome.Failed);
    }

    [Fact]
    public async Task FireWebHookAsync_EndpointAddressRefused_ReturnsRefusedAndLogsTheKeyNotTheUrl()
    {
        // Arrange
        const string url = "http://127.0.0.1:8080/hook?token=secret";
        var logger = new TextLogger();
        var httpClient = new HttpClient(new RefusingHttpHandler());
        var server = new WebHookServer(httpClient, logger);
        var instructions = new TaskInstructions(url) { FormId = 42 };

        // Act
        var result = await server.FireWebHookAsync(CreateTestWebHookMessage(), instructions, CancellationToken.None);

        // Assert
        result.Should().Be(WebHookDeliveryOutcome.Refused);
        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Should().Be(EndatixEventIds.Webhooks.EndpointRefused);
        entry.Exception.Should().BeNull();
        entry.Message.Should().Contain(WebHookEndpointKey.Of(url)).And.Contain("42").And.Contain("NonPublicAddress")
            .And.NotContain("127.0.0.1").And.NotContain("secret");
    }

    [Fact]
    public async Task FireWebHookAsync_WithApiKeyAuthentication_AddsCorrectHeaders()
    {
        // Arrange
        var handler = new MockHttpHandler(HttpStatusCode.OK, "Success");
        var httpClient = new HttpClient(handler);
        var server = new WebHookServer(httpClient, _logger);

        var message = CreateTestWebHookMessage();
        var auth = new AuthenticationConfig
        {
            Type = AuthenticationType.ApiKey,
            ApiKey = "test-api-key",
            ApiKeyHeader = "X-API-KEY"
        };
        var instructions = new TaskInstructions("https://api.example.com/webhooks", auth);

        // Act
        var result = await server.FireWebHookAsync(message, instructions, CancellationToken.None);

        // Assert
        result.Should().Be(WebHookDeliveryOutcome.Delivered);
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Should().Contain(h => h.Key == "X-API-KEY" && h.Value.First() == "test-api-key");
    }

    [Fact]
    public async Task FireWebHookAsync_WithNoAuthentication_DoesNotAddAuthHeaders()
    {
        // Arrange
        var handler = new MockHttpHandler(HttpStatusCode.OK, "Success");
        var httpClient = new HttpClient(handler);
        var server = new WebHookServer(httpClient, _logger);

        var message = CreateTestWebHookMessage();
        var instructions = new TaskInstructions("https://api.example.com/webhooks");

        // Act
        var result = await server.FireWebHookAsync(message, instructions, CancellationToken.None);

        // Assert
        result.Should().Be(WebHookDeliveryOutcome.Delivered);
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Should().NotContain(h => h.Key.StartsWith("X-API") || h.Key == "Authorization");
    }

    [Fact]
    public async Task FireWebHookAsync_WithNoneAuthentication_DoesNotAddAuthHeaders()
    {
        // Arrange
        var handler = new MockHttpHandler(HttpStatusCode.OK, "Success");
        var httpClient = new HttpClient(handler);
        var server = new WebHookServer(httpClient, _logger);

        var message = CreateTestWebHookMessage();
        var auth = new AuthenticationConfig { Type = AuthenticationType.None };
        var instructions = new TaskInstructions("https://api.example.com/webhooks", auth);

        // Act
        var result = await server.FireWebHookAsync(message, instructions, CancellationToken.None);

        // Assert
        result.Should().Be(WebHookDeliveryOutcome.Delivered);
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Should().NotContain(h => h.Key.StartsWith("X-API") || h.Key == "Authorization");
    }

    [Fact]
    public async Task FireWebHookAsync_AddsStandardWebHookHeaders()
    {
        // Arrange
        var handler = new MockHttpHandler(HttpStatusCode.OK, "Success");
        var httpClient = new HttpClient(handler);
        var server = new WebHookServer(httpClient, _logger);

        var message = CreateTestWebHookMessage();
        var instructions = new TaskInstructions("https://api.example.com/webhooks");

        // Act
        var result = await server.FireWebHookAsync(message, instructions, CancellationToken.None);

        // Assert
        result.Should().Be(WebHookDeliveryOutcome.Delivered);
        handler.LastRequest.Should().NotBeNull();
        
        var headers = handler.LastRequest!.Headers.ToDictionary(h => h.Key, h => h.Value.First());
        headers.Should().ContainKey("X-Endatix-Event");
        headers.Should().ContainKey("X-Endatix-Entity");
        headers.Should().ContainKey("X-Endatix-Action");
        headers.Should().ContainKey("X-Endatix-Hook-Id");
        
        headers["X-Endatix-Event"].Should().Be("submission_completed");
        headers["X-Endatix-Entity"].Should().Be("Submission");
        headers["X-Endatix-Action"].Should().Be("updated");
        headers["X-Endatix-Hook-Id"].Should().Be("123");
    }

    [Fact]
    public async Task FireWebHookAsync_SendsCorrectJsonPayload()
    {
        // Arrange
        var handler = new MockHttpHandler(HttpStatusCode.OK, "Success");
        var httpClient = new HttpClient(handler);
        var server = new WebHookServer(httpClient, _logger);

        var message = CreateTestWebHookMessage();
        var instructions = new TaskInstructions("https://api.example.com/webhooks");

        // Act
        var result = await server.FireWebHookAsync(message, instructions, CancellationToken.None);

        // Assert
        result.Should().Be(WebHookDeliveryOutcome.Delivered);
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Content.Should().NotBeNull();
        
        var content = handler.LastRequestContent;
        content.Should().NotBeNullOrEmpty();
        
        // Parse JSON without full deserialization to avoid constructor issues
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        root.GetProperty("id").GetString().Should().Be("123");
        root.GetProperty("eventName").GetString().Should().Be("submission_completed");
        root.GetProperty("action").GetString().Should().Be("updated");
        
        var payload = root.GetProperty("payload");
        payload.GetProperty("Name").GetString().Should().Be("Test");
    }

    [Fact]
    public async Task FireWebHookAsync_WithApiKeyAuthenticationMissingKey_ReturnsFailed()
    {
        // Arrange
        var handler = new MockHttpHandler(HttpStatusCode.OK, "Success");
        var httpClient = new HttpClient(handler);
        var server = new WebHookServer(httpClient, _logger);

        var message = CreateTestWebHookMessage();
        var auth = new AuthenticationConfig
        {
            Type = AuthenticationType.ApiKey,
            ApiKey = null, // Missing API key
            ApiKeyHeader = "X-API-KEY"
        };
        var instructions = new TaskInstructions("https://api.example.com/webhooks", auth);

        // Act
        var result = await server.FireWebHookAsync(message, instructions, CancellationToken.None);

        // Assert
        result.Should().Be(WebHookDeliveryOutcome.Failed);
    }

    [Fact]
    public async Task FireWebHookAsync_WithApiKeyAuthenticationMissingHeader_ReturnsFailed()
    {
        // Arrange
        var handler = new MockHttpHandler(HttpStatusCode.OK, "Success");
        var httpClient = new HttpClient(handler);
        var server = new WebHookServer(httpClient, _logger);

        var message = CreateTestWebHookMessage();
        var auth = new AuthenticationConfig
        {
            Type = AuthenticationType.ApiKey,
            ApiKey = "test-key",
            ApiKeyHeader = null // Missing header name
        };
        var instructions = new TaskInstructions("https://api.example.com/webhooks", auth);

        // Act
        var result = await server.FireWebHookAsync(message, instructions, CancellationToken.None);

        // Assert
        result.Should().Be(WebHookDeliveryOutcome.Failed);
    }

    private static WebHookMessage<TestPayload> CreateTestWebHookMessage()
    {
        var operation = WebHookOperation.SubmissionCompleted;
        var payload = new TestPayload { Name = "Test" };
        return new WebHookMessage<TestPayload>(123, operation, payload);
    }

    private class TestPayload
    {
        public string Name { get; set; } = string.Empty;
    }

    // Helper classes for HTTP mocking
    // Fails the way SocketsHttpHandler does when the connect callback refuses: the wrapper names the host and port.
    private sealed class RefusingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException(
                $"Connection refused ({request.RequestUri!.Host}:{request.RequestUri.Port})",
                new OutboundAddressRefusedException(OutboundAddressRefusalReason.NonPublicAddress));
    }

    private sealed class TextLogger : ILogger<WebHookServer>
    {
        public List<(int EventId, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((eventId.Id, formatter(state, exception), exception));
    }

    private class MockHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestContent { get; private set; }

        public MockHttpHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            
            // Capture the request content before it gets disposed
            if (request.Content != null)
            {
                LastRequestContent = await request.Content.ReadAsStringAsync();
            }
            
            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };
        }
    }

    private class ThrowingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Simulated network error");
        }
    }
} 