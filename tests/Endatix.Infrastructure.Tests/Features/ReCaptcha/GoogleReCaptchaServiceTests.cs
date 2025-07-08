using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.ReCaptcha;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.ReCaptcha;

public class GoogleReCaptchaServiceTests
{
    private readonly IUserContext _userContext;
    private readonly IOptions<ReCaptchaOptions> _options;

    public GoogleReCaptchaServiceTests()
    {
        _userContext = Substitute.For<IUserContext>();
        _options = Substitute.For<IOptions<ReCaptchaOptions>>();
    }

    private static readonly ReCaptchaOptions _defaultOptions = new()
    {
        IsEnabled = true,
        SecretKey = "secret",
        MinimumScore = 0.5,
        EnabledForTenantIds = [1]
    };

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
        _options.Value.Returns(options);
        var reCaptchaHttpClient = new StubReCaptchaHttpClient();

        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);

        // Act
        var result = service.IsEnabled;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task VerifyTokenAsync_ReturnsSuccessFallback_WhenDisabled()
    {
        // Arrange
        var disabledOptions = new ReCaptchaOptions { IsEnabled = false, SecretKey = "secret", MinimumScore = 0.5 };
        _options.Value.Returns(disabledOptions);

        var successValidationResponse = new GoogleReCaptchaResponse(true, DateTime.UtcNow, "localhost", 1.0, "form_submit", null);
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(successValidationResponse);

        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);

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
        _options.Value.Returns(_defaultOptions);
        var successValidationResponse = new GoogleReCaptchaResponse(true, DateTime.UtcNow, "localhost", 1.0, "form_submit", null);
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(successValidationResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);

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
        _options.Value.Returns(_defaultOptions);
        var invalidResponse = GoogleReCaptchaResponses.InvalidResponse();
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(invalidResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);

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
        _options.Value.Returns(_defaultOptions);
        var scoreTooLowResponse = GoogleReCaptchaResponses.ScoreTooLow(0.1);
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(scoreTooLowResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);

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
        _options.Value.Returns(_defaultOptions);
        var successResponse = GoogleReCaptchaResponses.Success();
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(successResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);

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
        _options.Value.Returns(_defaultOptions);
        var invalidResponse = GoogleReCaptchaResponses.InvalidResponse();
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(invalidResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);

        // Act
        var result = await service.VerifyTokenAsync("sometoken", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("invalid_response", result.ErrorCodes);
    }

    [Fact]
    public void RequiresReCaptcha_ThrowsArgumentNullException_WhenFormIsNull()
    {
        // Arrange
        _options.Value.Returns(_defaultOptions);
        var successResponse = GoogleReCaptchaResponses.Success();
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(successResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);

        Form form = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.RequiresReCaptcha(form));
    }

    [Fact]
    public void RequiresReCaptcha_ReturnsFalse_WhenIsEnabledIsFalse()
    {
        // Arrange
        var options = new ReCaptchaOptions { IsEnabled = false, SecretKey = "secret", MinimumScore = 0.5, EnabledForTenantIds = [1] };
        _options.Value.Returns(options);
        var successResponse = GoogleReCaptchaResponses.Success();
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(successResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);
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
        var options = new ReCaptchaOptions { IsEnabled = true, SecretKey = "secret", MinimumScore = 0.5, EnabledForTenantIds = [2, 3] };
        _options.Value.Returns(options);
        var successResponse = GoogleReCaptchaResponses.Success();
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(successResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);
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
        var options = new ReCaptchaOptions { IsEnabled = true, SecretKey = "secret", MinimumScore = 0.5, EnabledForTenantIds = [1, 2] };
        _options.Value.Returns(options);
        var successResponse = GoogleReCaptchaResponses.Success();
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(successResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);
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
        _options.Value.Returns(options);
        var successResponse = GoogleReCaptchaResponses.Success();
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(successResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);
        var form = new StubForm(1);

        // Act
        var result = service.RequiresReCaptcha(form);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateReCaptchaAsync_ReturnsSuccess_WhenNotRequired()
    {
        // Arrange
        _options.Value.Returns(_defaultOptions);
        var successResponse = GoogleReCaptchaResponses.Success();
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(successResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);
        var form = new StubForm(999); // not in enabled list
        var context = new SubmissionVerificationContext(form, false, null, null);

        // Act
        var result = await service.ValidateReCaptchaAsync(context, default);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ValidateReCaptchaAsync_ReturnsSuccess_WhenIsCompleteIsFalse()
    {
        // Arrange
        _options.Value.Returns(_defaultOptions);
        var successResponse = GoogleReCaptchaResponses.Success();
        var reCaptchaHttpClient = Substitute.For<IReCaptchaHttpClient>();
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);
        var form = new StubForm(1);
        var submissionIsComplete = false;
        var context = new SubmissionVerificationContext(form, submissionIsComplete, null, null);

        // Act
        var result = await service.ValidateReCaptchaAsync(context, default);

        // Assert
        Assert.True(result.IsSuccess);
        await reCaptchaHttpClient.DidNotReceive().GetTokenValidationResponseAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task ValidateReCaptchaAsync_ReturnsSuccess_WhenUserIsAuthenticated()
    {
        // Arrange
        _options.Value.Returns(_defaultOptions);
        var invalidResponse = GoogleReCaptchaResponses.InvalidResponse();
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(invalidResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);

        var formWithRecaptcha = new StubForm(_defaultOptions.EnabledForTenantIds!.FirstOrDefault());
        _userContext.IsAuthenticated.Returns(true);
        var validationContext = new SubmissionVerificationContext(formWithRecaptcha, false, null, null);

        // Act
        var result = await service.ValidateReCaptchaAsync(validationContext, default);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ValidateReCaptchaAsync_ReturnsInvalid_WhenTokenIsMissing()
    {
        // Arrange
        _options.Value.Returns(_defaultOptions);
        var invalidResponse = GoogleReCaptchaResponses.InvalidResponse();
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(invalidResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);
        var form = new StubForm(_defaultOptions.EnabledForTenantIds!.FirstOrDefault());
        var isComplete = true;
        string? token = null;
        var validationContext = new SubmissionVerificationContext(form, isComplete, null, token);

        // Act
        var result = await service.ValidateReCaptchaAsync(validationContext, default);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.ValidationErrors, e => e.ErrorMessage.Contains("ReCAPTCHA token is required"));
    }

    [Fact]
    public async Task ValidateReCaptchaAsync_ReturnsInvalid_WhenVerificationFails()
    {
        // Arrange
        _options.Value.Returns(_defaultOptions);
        var invalidResponse = GoogleReCaptchaResponses.InvalidResponse();
        var reCaptchaHttpClient = Substitute.For<IReCaptchaHttpClient>();
        reCaptchaHttpClient.GetTokenValidationResponseAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(invalidResponse)));

        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);
        var form = new StubForm(_defaultOptions.EnabledForTenantIds!.FirstOrDefault());
        var token = "invalid_token";
        var isComplete = true;
        var validationContext = new SubmissionVerificationContext(form, isComplete, null, token);

        // Act
        var result = await service.ValidateReCaptchaAsync(validationContext, default);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.ValidationErrors, e => e.ErrorMessage.Contains("reCAPTCHA validation failed"));
        await reCaptchaHttpClient.Received(1).GetTokenValidationResponseAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateReCaptchaAsync_ReturnsSuccess_WhenVerificationSucceeds()
    {
        // Arrange
        _options.Value.Returns(_defaultOptions);
        var successResponse = GoogleReCaptchaResponses.Success();
        var reCaptchaHttpClient = new StubReCaptchaHttpClient(successResponse);
        var service = new GoogleReCaptchaService(reCaptchaHttpClient, _options, _userContext);
        var form = new StubForm(_defaultOptions.EnabledForTenantIds!.FirstOrDefault());
        var token = "valid_token";
        var validationContext = new SubmissionVerificationContext(form, false, null, token);

        // Act
        var result = await service.ValidateReCaptchaAsync(validationContext, default);

        // Assert
        Assert.True(result.IsSuccess);
    }
}

internal class StubReCaptchaHttpClient : IReCaptchaHttpClient
{
    private readonly GoogleReCaptchaResponse _response;

    public StubReCaptchaHttpClient(GoogleReCaptchaResponse? response = null)
    {
        _response = response ?? GoogleReCaptchaResponses.Success();
    }

    public Task<Result<GoogleReCaptchaResponse>> GetTokenValidationResponseAsync(string token, string action, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(_response));
    }
}

internal static class GoogleReCaptchaResponses
{
    public static GoogleReCaptchaResponse Success(double score = 1.0) => new(true, DateTime.UtcNow, "localhost", score, "form_submit", null);
    public static GoogleReCaptchaResponse InvalidResponse() => new(false, DateTime.UtcNow, "localhost", 0.0, "form_submit", ["invalid_response"]);
    public static GoogleReCaptchaResponse ScoreTooLow(double score = 0.3) => new(true, DateTime.UtcNow, "localhost", score, "form_submit", null);
}

internal class StubForm : Form
{
    public StubForm(long tenantId) : base(tenantId, "TestForm") { }
}