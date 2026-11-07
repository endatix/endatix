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
/// Real siteverify bodies (<c>Fixtures/Siteverify</c>) through the real <see cref="ReCaptchaHttpClient"/>.
/// The action is checked before the score, so a key of the wrong type gets its own reason
/// instead of <c>score_too_low</c>.
/// </summary>
public class GoogleReCaptchaServiceReasonTests
{
    private readonly IUserContext _userContext = Substitute.For<IUserContext>();

    private const int NoLog = 0;

    [Theory]
    [InlineData("v3-submit-form.json", 0.5, true, null, LogLevel.None, NoLog)]
    [InlineData("v3-submit-form.json", 1.0, false, ReCaptchaConstants.ErrorCodes.ERROR_SCORE_TOO_LOW, LogLevel.Information, EndatixEventIds.ReCaptcha.ScoreTooLow)]
    [InlineData("v3-other-action.json", 0.5, true, null, LogLevel.Warning, EndatixEventIds.ReCaptcha.ActionMismatch)]
    [InlineData("v3-token-reused.json", 0.5, false, "timeout-or-duplicate", LogLevel.Information, EndatixEventIds.ReCaptcha.TokenRejected)]
    [InlineData("v3-wrong-secret.json", 0.5, false, "invalid-input-response", LogLevel.Information, EndatixEventIds.ReCaptcha.TokenRejected)]
    [InlineData("v2-test-key.json", 0.5, false, ReCaptchaConstants.ErrorCodes.ERROR_ACTION_MISSING, LogLevel.Warning, EndatixEventIds.ReCaptcha.ActionMissing)]
    [InlineData("v3-submit-form-without-action.derived.json", 0.5, false, ReCaptchaConstants.ErrorCodes.ERROR_ACTION_MISSING, LogLevel.Warning, EndatixEventIds.ReCaptcha.ActionMissing)]
    [InlineData("v3-submit-form-without-score.derived.json", 0.5, false, ReCaptchaConstants.ErrorCodes.ERROR_SCORE_MISSING, LogLevel.Warning, EndatixEventIds.ReCaptcha.ScoreMissing)]
    public async Task VerifyTokenAsync_NamesTheReason_ForARecordedSiteverifyBody(
        string fixture, double minimumScore, bool expectedSuccess, string? expectedFirstCode, LogLevel expectedLevel, int expectedEventId)
    {
        // Arrange
        var logger = new RecordingLogger<GoogleReCaptchaService>();
        var service = new GoogleReCaptchaService(SiteverifyFixture.Client(fixture), Options(minimumScore), _userContext, logger);

        // Act
        var result = await service.VerifyTokenAsync("recorded-token", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedSuccess, result.IsSuccess);
        Assert.Equal(expectedFirstCode, result.ErrorCodes.FirstOrDefault());
        Assert.Equal(expectedEventId == NoLog ? [] : [(expectedLevel, expectedEventId)], logger.Entries);
    }

    private static IOptions<ReCaptchaOptions> Options(double minimumScore) => Microsoft.Extensions.Options.Options.Create(new ReCaptchaOptions
    {
        IsEnabled = true,
        SecretKey = "secret",
        MinimumScore = minimumScore,
        EnabledForTenantIds = [1]
    });
}

/// <summary>Reads a recorded siteverify body and serves it the way Google does.</summary>
internal static class SiteverifyFixture
{
    public static string Body(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Features", "ReCaptcha", "Fixtures", "Siteverify", name));

    public static ReCaptchaHttpClient Client(string name) => new(
        new HttpClient(new FixedStatusHandler(HttpStatusCode.OK, Body(name))),
        NullLogger<ReCaptchaHttpClient>.Instance);
}
