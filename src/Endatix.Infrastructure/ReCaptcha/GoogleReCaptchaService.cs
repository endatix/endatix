using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Features.ReCaptcha;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.ReCaptcha;

/// <summary>
/// Implementation of <see cref="IReCaptchaPolicyService"/> for Google reCAPTCHA v3
/// </summary>
internal sealed class GoogleReCaptchaService : IReCaptchaPolicyService
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
            return ReCaptchaVerificationResult.InvalidResponse(0.0, ReCaptchaConstants.Actions.NO_ACTION_APPLICABLE, ReCaptchaConstants.ErrorCodes.ERROR_TOKEN_MISSING);
        }

        var googleReCaptchaTokenResult = await _reCaptchaClient.GetTokenValidationResponseAsync(token, _options.SecretKey, cancellationToken);

        if (!googleReCaptchaTokenResult.IsSuccess)
        {
            return ReCaptchaVerificationResult.InvalidResponse(0.0, ReCaptchaConstants.Actions.NO_ACTION_APPLICABLE, ReCaptchaConstants.ErrorCodes.ERROR_INVALID_RESPONSE);
        }

        var reCaptchaToken = googleReCaptchaTokenResult.Value;

        if (!reCaptchaToken.Success)
        {
            return ReCaptchaVerificationResult.InvalidResponse(0.0, reCaptchaToken.Action, reCaptchaToken.ErrorCodes ?? []);
        }

        if (reCaptchaToken.Score < _options.MinimumScore)
        {
            return ReCaptchaVerificationResult.InvalidResponse(reCaptchaToken.Score, reCaptchaToken.Action, [ReCaptchaConstants.ErrorCodes.ERROR_SCORE_TOO_LOW, $"{reCaptchaToken.Score} < {_options.MinimumScore}"]);
        }

        return ReCaptchaVerificationResult.Success(reCaptchaToken.Score, reCaptchaToken.Action);
    }

    public bool IsEnabled { get; }

    /// <inheritdoc/>
    public bool RequiresReCaptcha(Form form)
    {
        Guard.Against.Null(form);
        if (!IsEnabled)
        {
            return false;
        }

        return _options.EnabledForTenantIds?.Contains(form.TenantId) ?? false;
    }
}