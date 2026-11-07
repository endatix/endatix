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

    [Theory]
    [InlineData("v2-test-key.json", 0.5, false, ReCaptchaConstants.ErrorCodes.ERROR_ACTION_MISSING, LogLevel.Warning, EndatixEventIds.ReCaptcha.ActionMissing)]
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
        Assert.Equal([(expectedLevel, expectedEventId)], logger.Entries);
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
