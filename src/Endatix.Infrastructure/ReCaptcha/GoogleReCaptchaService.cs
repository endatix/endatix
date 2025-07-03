using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.ReCaptcha;

internal sealed class GoogleReCaptchaService : IGoogleReCaptchaService
{
    private readonly IReCaptchaHttpClient _reCaptchaClient;
    private readonly ReCaptchaOptions _options;

    public GoogleReCaptchaService(IReCaptchaHttpClient reCaptchaClient, IOptions<ReCaptchaOptions> options)
    {
        _reCaptchaClient = reCaptchaClient;
        _options = options.Value;
        IsEnabled = _options.IsEnabled && AreReCaptchaOptionsValid(_options);
    }

    private static bool AreReCaptchaOptionsValid(ReCaptchaOptions options) =>
        !string.IsNullOrEmpty(options.SecretKey)
        && options.MinimumScore > 0.0
        && options.MinimumScore <= 1.0;

    public async Task<ReCaptchaVerificationResult> VerifyTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return ReCaptchaVerificationResult.NotEnabled();
        }

        if (string.IsNullOrEmpty(token))
        {
            return ReCaptchaVerificationResult.InvalidResponse(0.0, ReCaptchaConstants.NO_ACTION_APPLICABLE, ReCaptchaConstants.ERROR_TOKEN_MISSING);
        }

        var googleReCaptchaTokenResult = await _reCaptchaClient.GetTokenValidationResponseAsync(token, _options.SecretKey, cancellationToken);

        if (!googleReCaptchaTokenResult.IsSuccess)
        {
            return ReCaptchaVerificationResult.InvalidResponse(0.0, ReCaptchaConstants.NO_ACTION_APPLICABLE, ReCaptchaConstants.ERROR_INVALID_RESPONSE);
        }

        var reCaptchaToken = googleReCaptchaTokenResult.Value;

        if (!reCaptchaToken.Success)
        {
            return ReCaptchaVerificationResult.InvalidResponse(0.0, reCaptchaToken.Action, reCaptchaToken.ErrorCodes ?? []);
        }

        if (reCaptchaToken.Score < _options.MinimumScore)
        {
            return ReCaptchaVerificationResult.InvalidResponse(reCaptchaToken.Score, reCaptchaToken.Action, [ReCaptchaConstants.ERROR_SCORE_TOO_LOW, $"{reCaptchaToken.Score} < {_options.MinimumScore}"]);
        }

        return ReCaptchaVerificationResult.Success(reCaptchaToken.Score, reCaptchaToken.Action);
    }

    public bool IsEnabled { get; }
}