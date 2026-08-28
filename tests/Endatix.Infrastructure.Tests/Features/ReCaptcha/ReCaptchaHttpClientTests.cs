using System.Net;
using Endatix.Infrastructure.ReCaptcha;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Tests.Features.ReCaptcha;

public class ReCaptchaHttpClientTests
{
    private static ReCaptchaHttpClient CreateClient(HttpMessageHandler? handler = null)
        => new(
            handler is null ? new HttpClient() : new HttpClient(handler),
            NullLogger<ReCaptchaHttpClient>.Instance);

    [Fact]
    public async Task ReturnsError_WhenTokenIsMissing()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var result = await client.GetTokenValidationResponseAsync(null!, "secret", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Token is required", result.Errors.FirstOrDefault());
    }

    [Fact]
    public async Task ReturnsError_WhenSecretIsMissing()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var result = await client.GetTokenValidationResponseAsync("token", null!, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Secret key is required", result.Errors);
    }

    [Fact]
    public async Task ReturnsError_WhenHttpThrows()
    {
        // Arrange
        var handler = new ThrowingHandler();
        var client = CreateClient(handler);

        // Act
        var result = await client.GetTokenValidationResponseAsync("token", "secret", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Could not reach the reCAPTCHA verification service.", result.Errors.FirstOrDefault());
    }

    [Fact]
    public async Task ReturnsError_WhenHttpStatusIsNotSuccess()
    {
        // Arrange
        var handler = new StatusCodeHandler(HttpStatusCode.BadRequest, "{}");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetTokenValidationResponseAsync("token", "secret", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("The reCAPTCHA verification service rejected the request.", result.Errors.FirstOrDefault());
    }

    /// <summary>
    /// A transport failure and a refusal by Google are different operational problems - one is our egress,
    /// the other our secret key or quota - so they must not collapse into one indistinguishable string.
    /// </summary>
    [Fact]
    public async Task ReturnsError_DistinguishesTransportFailureFromServiceRejection()
    {
        // Arrange
        var throwingClient = CreateClient(new ThrowingHandler());
        var rejectingClient = CreateClient(new StatusCodeHandler(HttpStatusCode.BadRequest, "{}"));

        // Act
        var transportResult = await throwingClient.GetTokenValidationResponseAsync("token", "secret", CancellationToken.None);
        var rejectionResult = await rejectingClient.GetTokenValidationResponseAsync("token", "secret", CancellationToken.None);

        // Assert
        Assert.False(transportResult.IsSuccess);
        Assert.False(rejectionResult.IsSuccess);
        Assert.NotEqual(transportResult.Errors.FirstOrDefault(), rejectionResult.Errors.FirstOrDefault());
    }

    [Fact]
    public async Task ReturnsError_WhenDeserializationFails()
    {
        // Arrange
        var handler = new StatusCodeHandler(HttpStatusCode.OK, "not a json");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetTokenValidationResponseAsync("token", "secret", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to deserialize Google ReCaptcha response", result.Errors.FirstOrDefault());
    }

    [Fact]
    public async Task ReturnsSuccess_WhenValid()
    {
        // Arrange
        var json = "{" +
            "\"success\":true," +
            "\"challenge_ts\":\"2025-07-03T09:20:11Z\"," +
            "\"hostname\":\"localhost\"," +
            "\"score\":0.9," +
            "\"action\":\"form_submit\"}";
        var handler = new StatusCodeHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetTokenValidationResponseAsync("token", "secret", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Success);
    }

    // Helper handlers for simulating HTTP responses
    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Simulated network error");
    }

    private class StatusCodeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;
        public StatusCodeHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content)
            });
    }
}