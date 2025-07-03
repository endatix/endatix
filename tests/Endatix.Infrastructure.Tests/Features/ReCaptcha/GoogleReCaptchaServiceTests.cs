using Endatix.Core.Entities;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.ReCaptcha;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.ReCaptcha;

public class GoogleReCaptchaServiceTests
{
    private static ReCaptchaOptions DefaultOptions => new()
    {
        IsEnabled = true,
        SecretKey = "secret",
        MinimumScore = 0.5
    };

    private static IOptions<ReCaptchaOptions> Options(ReCaptchaOptions? opts = null)
    {
        var options = Substitute.For<IOptions<ReCaptchaOptions>>();
        options.Value.Returns(opts ?? DefaultOptions);
        return options;
    }

    private static IReCaptchaHttpClient MockHttpClient(GoogleReCaptchaResponse response)
    {
        var client = Substitute.For<IReCaptchaHttpClient>();
        client.GetTokenValidationResponseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(response)));
        return client;
    }

    [Theory]
    [InlineData(false, "secret", 0.5, false)] // IsEnabled false
    [InlineData(true, null, 0.5, false)]      // SecretKey null
    [InlineData(true, "", 0.5, false)]       // SecretKey empty
    [InlineData(true, "secret", 0.0, false)] // MinimumScore zero
    [InlineData(true, "secret", -0.1, false)]// MinimumScore negative
    [InlineData(true, "secret", 1.1, false)] // MinimumScore > 1
    [InlineData(true, "secret", 0.5, true)]  // All valid
    public void IsEnabled_ValidationCases(bool enabled, string secret, double minScore, bool expected)
    {
        // Arrange
        var options = new ReCaptchaOptions { IsEnabled = enabled, SecretKey = secret, MinimumScore = minScore };
        var service = new GoogleReCaptchaService(MockHttpClient(StubGoogleReCaptchaResponses.Success()), Options(options));

        // Act
        var result = service.IsEnabled;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task VerifyTokenAsync_ReturnsSuccessFallback_WhenDisabled()
    {
        // Arrange
        var options = new ReCaptchaOptions { IsEnabled = false, SecretKey = "secret", MinimumScore = 0.5 };
        var service = new GoogleReCaptchaService(MockHttpClient(new GoogleReCaptchaResponse(true, DateTime.UtcNow, "localhost", 1.0, "form_submit", null)), Options(options));

        // Act
        var result = await service.VerifyTokenAsync("sometoken", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1.0, result.Score);
        Assert.Equal(ReCaptchaConstants.Actions.NO_ACTION_APPLICABLE, result.Action);
        Assert.Contains("reCAPTCHA is not enabled", result.ErrorCodes);
    }

    [Fact]
    public async Task VerifyTokenAsync_ReturnsTokenMissing_WhenTokenIsNullOrEmpty()
    {
        // Arrange
        var mockResponse = StubGoogleReCaptchaResponses.Success();
        var httpClient = MockHttpClient(mockResponse);
        var service = new GoogleReCaptchaService(httpClient, Options());

        // Act
        var result = await service.VerifyTokenAsync(null!, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("token_missing", result.ErrorCodes);
    }

    [Fact]
    public async Task VerifyTokenAsync_ReturnsInvalidResponse_WhenGoogleResponseIsInvalid()
    {
        // Arrange
        var mockResponse = StubGoogleReCaptchaResponses.InvalidResponse();
        var httpClient = MockHttpClient(mockResponse);
        var service = new GoogleReCaptchaService(httpClient, Options());

        // Act
        var result = await service.VerifyTokenAsync("sometoken", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("invalid_response", result.ErrorCodes);
    }

    [Fact]
    public async Task VerifyTokenAsync_ReturnsScoreTooLow_WhenScoreIsBelowMinimum()
    {
        // Arrange
        var mockResponse = StubGoogleReCaptchaResponses.ScoreTooLow(0.1);
        var httpClient = MockHttpClient(mockResponse);
        var service = new GoogleReCaptchaService(httpClient, Options());

        // Act
        var result = await service.VerifyTokenAsync("sometoken", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("score_too_low", result.ErrorCodes);
    }

    [Fact]
    public async Task VerifyTokenAsync_ReturnsSuccess_WhenValid()
    {
        // Arrange
        var mockResponse = StubGoogleReCaptchaResponses.Success();
        var httpClient = MockHttpClient(mockResponse);
        var service = new GoogleReCaptchaService(httpClient, Options());

        // Act
        var result = await service.VerifyTokenAsync("sometoken", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.ErrorCodes);
    }

    [Fact]
    public async Task VerifyTokenAsync_ReturnsInvalidResponse_WhenHttpResponseIsNotSuccess()
    {
        // Arrange
        var mockResponse = StubGoogleReCaptchaResponses.InvalidResponse();
        var httpClient = MockHttpClient(mockResponse);
        var service = new GoogleReCaptchaService(httpClient, Options());

        // Act
        var result = await service.VerifyTokenAsync("sometoken", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("invalid_response", result.ErrorCodes);
    }

    private class StubGoogleReCaptchaResponses
    {
        public static GoogleReCaptchaResponse Success(double score = 1.0) => new(true, DateTime.UtcNow, "localhost", score, "form_submit", null);
        public static GoogleReCaptchaResponse InvalidResponse() => new(false, DateTime.UtcNow, "localhost", 0.0, "form_submit", ["invalid_response"]);
        public static GoogleReCaptchaResponse ScoreTooLow(double score = 0.3) => new(true, DateTime.UtcNow, "localhost", score, "form_submit", null);
    }

    public class StubForm : Form
    {
        public StubForm(long tenantId) : base(tenantId, "TestForm") { }
    }

    [Fact]
    public void RequiresReCaptcha_ThrowsArgumentNullException_WhenFormIsNull()
    {
        // Arrange
        var service = new GoogleReCaptchaService(MockHttpClient(StubGoogleReCaptchaResponses.Success()), Options());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.RequiresReCaptcha(null!));
    }

    [Fact]
    public void RequiresReCaptcha_ReturnsFalse_WhenIsEnabledIsFalse()
    {
        // Arrange
        var options = new ReCaptchaOptions { IsEnabled = false, SecretKey = "secret", MinimumScore = 0.5, EnabledForTenantIds = new long[] { 1 } };
        var service = new GoogleReCaptchaService(MockHttpClient(StubGoogleReCaptchaResponses.Success()), Options(options));
        var form = new StubForm(1);

        // Act
        var result = service.RequiresReCaptcha(form);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RequiresReCaptcha_ReturnsFalse_WhenTenantNotInEnabledList()
    {
        // Arrange
        var options = new ReCaptchaOptions { IsEnabled = true, SecretKey = "secret", MinimumScore = 0.5, EnabledForTenantIds = new long[] { 2, 3 } };
        var service = new GoogleReCaptchaService(MockHttpClient(StubGoogleReCaptchaResponses.Success()), Options(options));
        var form = new StubForm(1);

        // Act
        var result = service.RequiresReCaptcha(form);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RequiresReCaptcha_ReturnsTrue_WhenTenantInEnabledList()
    {
        // Arrange
        var options = new ReCaptchaOptions { IsEnabled = true, SecretKey = "secret", MinimumScore = 0.5, EnabledForTenantIds = new long[] { 1, 2 } };
        var service = new GoogleReCaptchaService(MockHttpClient(StubGoogleReCaptchaResponses.Success()), Options(options));
        var form = new StubForm(1);

        // Act
        var result = service.RequiresReCaptcha(form);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RequiresReCaptcha_ReturnsFalse_WhenEnabledForTenantIdsIsNull()
    {
        // Arrange
        var options = new ReCaptchaOptions { IsEnabled = true, SecretKey = "secret", MinimumScore = 0.5, EnabledForTenantIds = null };
        var service = new GoogleReCaptchaService(MockHttpClient(StubGoogleReCaptchaResponses.Success()), Options(options));
        var form = new StubForm(1);

        // Act
        var result = service.RequiresReCaptcha(form);

        // Assert
        Assert.False(result);
    }
}