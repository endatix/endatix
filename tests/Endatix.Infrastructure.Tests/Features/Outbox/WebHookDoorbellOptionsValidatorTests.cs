using Endatix.Infrastructure.Features.Outbox;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

public class WebHookDoorbellOptionsValidatorTests
{
    [Theory]
    [InlineData("WorkerBaseUrl", "localhost:8081", "0123456789abcdef0123456789abcdef", 10)]
    [InlineData("WorkerApiKey", "http://localhost:8081", "0123456789abcdef0123456789abcde", 10)]
    [InlineData("TimeoutSeconds", "http://localhost:8081", "0123456789abcdef0123456789abcdef", 0)]
    public void Validate_EnabledWithInvalidKey_FailsNamingTheKey(string key, string workerBaseUrl, string workerApiKey, int timeoutSeconds)
    {
        // Arrange
        var options = new WebHookDoorbellOptions
        {
            Enabled = true,
            WorkerBaseUrl = workerBaseUrl,
            WorkerApiKey = workerApiKey,
            TimeoutSeconds = timeoutSeconds,
        };

        // Act
        var result = new WebHookDoorbellOptionsValidator().Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(key);
    }

    [Fact]
    public void Validate_DisabledWithoutOtherKeys_Succeeds()
    {
        // Arrange
        var options = new WebHookDoorbellOptions { Enabled = false };

        // Act
        var result = new WebHookDoorbellOptionsValidator().Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }
}
