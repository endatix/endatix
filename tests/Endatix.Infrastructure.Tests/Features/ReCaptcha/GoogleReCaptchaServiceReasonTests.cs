using System.Net;
using Endatix.Core.Abstractions;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Framework.Logging;
using Endatix.Infrastructure.ReCaptcha;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.ReCaptcha;

/// <summary>
/// A token that siteverify accepts is checked for its action before its score, so a key of the
/// wrong type gets its own reason instead of <c>score_too_low</c>.
/// </summary>
public class GoogleReCaptchaServiceReasonTests
{
    private readonly IUserContext _userContext = Substitute.For<IUserContext>();
    private readonly IOptions<ReCaptchaOptions> _options = Options.Create(new ReCaptchaOptions
    {
        IsEnabled = true,
        SecretKey = "secret",
        MinimumScore = 0.5,
        EnabledForTenantIds = [1]
    });

    [Fact]
    public async Task VerifyTokenAsync_ReturnsActionMissing_WhenSiteverifyReturnsV2Shape()
    {
        // Arrange
        var json = """{"success":true,"challenge_ts":"2026-11-04T10:00:00Z","hostname":"localhost"}""";
        var httpClient = new ReCaptchaHttpClient(
            new HttpClient(new FixedStatusHandler(HttpStatusCode.OK, json)),
            NullLogger<ReCaptchaHttpClient>.Instance);
        var logger = new RecordingLogger<GoogleReCaptchaService>();
        var service = new GoogleReCaptchaService(httpClient, _options, _userContext, logger);

        // Act
        var result = await service.VerifyTokenAsync("sometoken", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal([ReCaptchaConstants.ErrorCodes.ERROR_ACTION_MISSING], result.ErrorCodes);
        Assert.Equal([(LogLevel.Warning, EndatixEventIds.ReCaptcha.ActionMissing)], logger.Entries);
    }

    [Fact]
    public async Task VerifyTokenAsync_ReturnsActionMissing_NotScoreTooLow_WhenTokenHasNoActionAndZeroScore()
    {
        // Arrange
        var noAction = new GoogleReCaptchaResponse(true, DateTime.UtcNow, "localhost", 0.0, null, null);
        var service = new GoogleReCaptchaService(new StubReCaptchaHttpClient(noAction), _options, _userContext);

        // Act
        var result = await service.VerifyTokenAsync("sometoken", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([ReCaptchaConstants.ErrorCodes.ERROR_ACTION_MISSING], result.ErrorCodes);
    }

    [Fact]
    public async Task VerifyTokenAsync_ReturnsActionMismatch_WhenActionIsNotSubmitForm()
    {
        // Arrange
        var wrongAction = new GoogleReCaptchaResponse(true, DateTime.UtcNow, "localhost", 0.9, ReCaptchaConstants.Actions.SIGN_IN, null);
        var logger = new RecordingLogger<GoogleReCaptchaService>();
        var service = new GoogleReCaptchaService(new StubReCaptchaHttpClient(wrongAction), _options, _userContext, logger);

        // Act
        var result = await service.VerifyTokenAsync("sometoken", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ReCaptchaConstants.Actions.SIGN_IN, result.Action);
        Assert.Equal([ReCaptchaConstants.ErrorCodes.ERROR_ACTION_MISMATCH], result.ErrorCodes);
        Assert.Equal([(LogLevel.Warning, EndatixEventIds.ReCaptcha.ActionMismatch)], logger.Entries);
    }

    [Fact]
    public async Task VerifyTokenAsync_ReturnsScoreMissing_WhenSuccessfulTokenHasNoScore()
    {
        // Arrange
        var noScore = new GoogleReCaptchaResponse(true, DateTime.UtcNow, "localhost", null, ReCaptchaConstants.Actions.SUBMIT_FORM, null);
        var logger = new RecordingLogger<GoogleReCaptchaService>();
        var service = new GoogleReCaptchaService(new StubReCaptchaHttpClient(noScore), _options, _userContext, logger);

        // Act
        var result = await service.VerifyTokenAsync("sometoken", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal([ReCaptchaConstants.ErrorCodes.ERROR_SCORE_MISSING], result.ErrorCodes);
        Assert.Equal([(LogLevel.Warning, EndatixEventIds.ReCaptcha.ScoreMissing)], logger.Entries);
    }
}
