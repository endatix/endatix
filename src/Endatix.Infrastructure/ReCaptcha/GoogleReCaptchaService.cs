using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.ReCaptcha;

/// <summary>
/// Implementation of <see cref="IReCaptchaPolicyService"/> for Google reCAPTCHA v3
/// </summary>
internal sealed class GoogleReCaptchaService : IReCaptchaPolicyService
{
    /// <summary>
    /// siteverify error codes that mean our configuration or request is wrong, not the visitor's token.
    /// Everything else (expired, duplicate or forged tokens) is expected traffic.
    /// </summary>
    private static readonly HashSet<string> _configurationErrorCodes = new(StringComparer.Ordinal)
    {
        "missing-input-secret",
        "invalid-input-secret",
        "bad-request",
    };

    /// <summary>
    /// The action Hub passes to <c>grecaptcha.execute</c> on a public form completion. A token
    /// issued for another action still passes, with a warning.
    /// </summary>
    private const string EXPECTED_ACTION = ReCaptchaConstants.Actions.SUBMIT_FORM;

    private readonly IReCaptchaHttpClient _reCaptchaClient;
    private readonly ReCaptchaOptions _options;
    private readonly IUserContext _userContext;
    private readonly ILogger<GoogleReCaptchaService> _logger;

    public GoogleReCaptchaService(
        IReCaptchaHttpClient reCaptchaClient,
        IOptions<ReCaptchaOptions> options,
        IUserContext userContext,
        ILogger<GoogleReCaptchaService>? logger = null)
    {
        _reCaptchaClient = reCaptchaClient;
        _options = options.Value;
        IsEnabled = _options.IsEnabled && AreReCaptchaOptionsValid(_options);
        _userContext = userContext;
        _logger = logger ?? NullLogger<GoogleReCaptchaService>.Instance;
    }


    /// <inheritdoc/>
    public async Task<Result> ValidateReCaptchaAsync(SubmissionVerificationContext context, CancellationToken cancellationToken)
    {
        Guard.Against.Null(context);

        if (!context.IsComplete)
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

        return Evaluate(googleReCaptchaTokenResult.Value);
    }

    /// <summary>
    /// Checks the siteverify payload in the order that names the cause: action before score,
    /// so a key of the wrong type is not reported as a low score.
    /// </summary>
    private ReCaptchaVerificationResult Evaluate(GoogleReCaptchaResponse token)
    {
        if (token is { Success: true, Action: { Length: > 0 } issuedFor and not EXPECTED_ACTION })
        {
            _logger.LogReCaptchaActionMismatch(issuedFor, EXPECTED_ACTION, token.Hostname);
        }

        return token switch
        {
            { Success: false } => Rejected(token),
            { Action: null or "" } => ActionMissing(token),
            { Action: var action, Score: null } => ScoreMissing(token, action),
            { Action: var action, Score: { } score } when score < _options.MinimumScore => ScoreTooLow(action, score),
            { Action: var action, Score: { } score } => ReCaptchaVerificationResult.Success(score, action),
        };
    }

    private ReCaptchaVerificationResult Rejected(GoogleReCaptchaResponse token)
    {
        var errorCodes = token.ErrorCodes ?? [];
        var action = token.Action ?? ReCaptchaConstants.Actions.NO_ACTION_APPLICABLE;
        var joinedCodes = string.Join(",", errorCodes);
        if (errorCodes.Any(_configurationErrorCodes.Contains))
        {
            _logger.LogReCaptchaConfigurationError(action, joinedCodes);
        }
        else
        {
            _logger.LogReCaptchaTokenRejected(action, joinedCodes);
        }

        return ReCaptchaVerificationResult.InvalidResponse(0.0, token.Action, errorCodes);
    }

    /// <summary>
    /// v3 always returns <c>action</c> with a successful token. Without it the token was not
    /// issued by <c>grecaptcha.execute</c> for this site key (for example a v2 key), so it is rejected.
    /// </summary>
    private ReCaptchaVerificationResult ActionMissing(GoogleReCaptchaResponse token)
    {
        _logger.LogReCaptchaActionMissing(token.Hostname);
        return ReCaptchaVerificationResult.InvalidResponse(
            token.Score ?? 0.0,
            ReCaptchaConstants.Actions.NO_ACTION_APPLICABLE,
            ReCaptchaConstants.ErrorCodes.ERROR_ACTION_MISSING);
    }

    /// <summary>
    /// v3 always returns <c>score</c> with a successful token, so a missing score points at a
    /// key of the wrong type, not at the visitor.
    /// </summary>
    private ReCaptchaVerificationResult ScoreMissing(GoogleReCaptchaResponse token, string action)
    {
        _logger.LogReCaptchaScoreMissing(action, token.Hostname);
        return ReCaptchaVerificationResult.InvalidResponse(
            0.0,
            action,
            ReCaptchaConstants.ErrorCodes.ERROR_SCORE_MISSING);
    }

    private ReCaptchaVerificationResult ScoreTooLow(string action, double score)
    {
        _logger.LogReCaptchaScoreTooLow(score, _options.MinimumScore, action);
        return ReCaptchaVerificationResult.InvalidResponse(
            score,
            action,
            [ReCaptchaConstants.ErrorCodes.ERROR_SCORE_TOO_LOW, $"{score} < {_options.MinimumScore}"]);
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
