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

    private const string Passed = "passed";
    private const string PassedWithActionWarning = "passed, action mismatch logged";

    [Theory]
    [InlineData("v3-submit-form.json", 0.5, Passed)]
    [InlineData("v3-submit-form.json", 1.0, ReCaptchaConstants.ErrorCodes.ERROR_SCORE_TOO_LOW)]
    [InlineData("v3-other-action.json", 0.5, PassedWithActionWarning)]
    [InlineData("v3-token-reused.json", 0.5, "timeout-or-duplicate")]
    [InlineData("v3-wrong-secret.json", 0.5, "invalid-input-response")]
    [InlineData("v2-test-key.json", 0.5, ReCaptchaConstants.ErrorCodes.ERROR_ACTION_MISSING)]
    [InlineData("v3-submit-form-without-action.derived.json", 0.5, ReCaptchaConstants.ErrorCodes.ERROR_ACTION_MISSING)]
    [InlineData("v3-submit-form-without-score.derived.json", 0.5, ReCaptchaConstants.ErrorCodes.ERROR_SCORE_MISSING)]
    public async Task VerifyTokenAsync_NamesTheReason_ForARecordedSiteverifyBody(string fixture, double minimumScore, string expectedReason)
    {
        // Arrange
        var logger = new RecordingLogger<GoogleReCaptchaService>();
        var service = new GoogleReCaptchaService(SiteverifyFixture.Client(fixture), Options(minimumScore), _userContext, logger);
        var (expectedSuccess, expectedLog) = Expected(expectedReason);

        // Act
        var result = await service.VerifyTokenAsync("recorded-token", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedSuccess, result.IsSuccess);
        Assert.Equal(expectedSuccess ? null : expectedReason, result.ErrorCodes.FirstOrDefault());
        Assert.Equal(expectedLog is { } entry ? [entry] : [], logger.Entries);
    }

    /// <summary>Whether a reason passes, and the one log entry it writes. Any other reason is Google's own error code.</summary>
    private static (bool Success, (LogLevel, int)? Log) Expected(string reason) => reason switch
    {
        Passed => (true, null),
        PassedWithActionWarning => (true, (LogLevel.Warning, EndatixEventIds.ReCaptcha.ActionMismatch)),
        ReCaptchaConstants.ErrorCodes.ERROR_SCORE_TOO_LOW => (false, (LogLevel.Information, EndatixEventIds.ReCaptcha.ScoreTooLow)),
        ReCaptchaConstants.ErrorCodes.ERROR_ACTION_MISSING => (false, (LogLevel.Warning, EndatixEventIds.ReCaptcha.ActionMissing)),
        ReCaptchaConstants.ErrorCodes.ERROR_SCORE_MISSING => (false, (LogLevel.Warning, EndatixEventIds.ReCaptcha.ScoreMissing)),
        _ => (false, (LogLevel.Information, EndatixEventIds.ReCaptcha.TokenRejected)),
    };

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
