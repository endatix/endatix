using Ardalis.GuardClauses;

namespace Endatix.Infrastructure.ReCaptcha;

/// <summary>
/// Represents the result of a reCAPTCHA verification.
/// </summary>
public record ReCaptchaVerificationResult
{
    /// <summary>
    /// Indicates if the verification was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// The action the reCAPTCHA token was used for.
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// The score assigned to the reCAPTCHA token.
    /// </summary>
    public double Score { get; }

    /// <summary>
    /// The action the reCAPTCHA token was used for.
    /// </summary>
    public string[] ErrorCodes { get; }

    private ReCaptchaVerificationResult(bool success, double score, string action, string[] errorCodes)
    {
        Guard.Against.Null(success, nameof(success));
        Guard.Against.Null(score, nameof(score));
        Guard.Against.Null(action, nameof(action));
        Guard.Against.Null(errorCodes, nameof(errorCodes));

        IsSuccess = success;
        Score = score;
        Action = action;
        ErrorCodes = errorCodes;
    }


    public static ReCaptchaVerificationResult Success(double score, string action)
    {
        return new ReCaptchaVerificationResult(true, score, action, Array.Empty<string>());
    }


    public static ReCaptchaVerificationResult InvalidResponse(double achievedScore, string action, string[]? errorMessages)
    {
        return new ReCaptchaVerificationResult(false, achievedScore, action, errorMessages ?? Array.Empty<string>());
    }

    public static ReCaptchaVerificationResult InvalidResponse(double achievedScore, string action, string errorMessage)
    {
        return new ReCaptchaVerificationResult(false, achievedScore, action, [errorMessage]);
    }

    public static ReCaptchaVerificationResult NotEnabled()
    {
        return new ReCaptchaVerificationResult(true, 1.0, ReCaptchaConstants.NO_ACTION_APPLICABLE, ["reCAPTCHA is not enabled"]);
    }

    public static ReCaptchaVerificationResult SystemError(string errorMessage)
    {
        return new ReCaptchaVerificationResult(false, 0.0, ReCaptchaConstants.NO_ACTION_APPLICABLE, [errorMessage, ReCaptchaConstants.ERROR_SYSTEM_ERROR]);
    }

    public static ReCaptchaVerificationResult SystemError(string[] errorMessages)
    {
        return new ReCaptchaVerificationResult(false, 0.0, ReCaptchaConstants.NO_ACTION_APPLICABLE, errorMessages);
    }

    public static ReCaptchaVerificationResult Skipped()
    {
        return new ReCaptchaVerificationResult(true, 1.0, ReCaptchaConstants.NO_ACTION_APPLICABLE, ["reCAPTCHA verification skipped"]);
    }

}