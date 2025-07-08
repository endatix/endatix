using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.ReCaptcha;

/// <summary>
/// Implementation of <see cref="IReCaptchaPolicyService"/> for Google reCAPTCHA v3
/// </summary>
internal sealed class GoogleReCaptchaService : IReCaptchaPolicyService
{
    private readonly IReCaptchaHttpClient _reCaptchaClient;
    private readonly ReCaptchaOptions _options;
    private readonly IUserContext _userContext;
    public GoogleReCaptchaService(
        IReCaptchaHttpClient reCaptchaClient,
        IOptions<ReCaptchaOptions> options,
        IUserContext userContext)
    {
        _reCaptchaClient = reCaptchaClient;
        _options = options.Value;
        IsEnabled = _options.IsEnabled && AreReCaptchaOptionsValid(_options);
        _userContext = userContext;
    }


    /// <inheritdoc/>
    public async Task<Result> ValidateReCaptchaAsync(SubmissionVerificationContext context, CancellationToken cancellationToken)
    {
        Guard.Against.Null(context);

        if (context.IsComplete == false)
        {
            return Result.Success();
        }

        if (!RequiresReCaptcha(context.Form))
        {
            return Result.Success();
        }

        if (_userContext.IsAuthenticated)
        {
            return Result.SuccessWithMessage("ReCAPTCHA is not required for authenticated users");
        }

        if (string.IsNullOrEmpty(context.ReCaptchaToken))
        {
            return Result.Invalid(new ValidationError("ReCAPTCHA token is required"));
        }

        var recaptchaVerificationResult = await VerifyTokenAsync(context.ReCaptchaToken, cancellationToken);
        if (!recaptchaVerificationResult.IsSuccess)
        {
            return Result.Invalid(new ValidationError("reCAPTCHA validation failed"));
        }

        return Result.Success();
    }

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


    private static bool AreReCaptchaOptionsValid(ReCaptchaOptions options) =>
        !string.IsNullOrEmpty(options.SecretKey)
        && options.MinimumScore > 0.0
        && options.MinimumScore <= 1.0;
}