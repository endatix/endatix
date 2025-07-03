namespace Endatix.Core.Features.ReCaptcha;

/// <summary>
/// Constants for reCAPTCHA verification
/// </summary>
public static class ReCaptchaConstants
{
    /// <summary>
    /// Actions for reCAPTCHA verification
    /// </summary>
    public static class Actions
    {
        public const string SUBMIT_FORM = "submit_form";
        public const string SIGN_UP = "sign_up";
        public const string SIGN_IN = "sign_in";
        public const string FORGOT_PASSWORD = "forgot_password";
        public const string RESET_PASSWORD = "reset_password";
        public const string NO_ACTION_APPLICABLE = "N/A";
    }

    /// <summary>
    /// Error codes for reCAPTCHA verification
    /// </summary>
    public static class ErrorCodes
    {
        public const string ERROR_TOKEN_MISSING = "token_missing";
        public const string ERROR_INVALID_RESPONSE = "invalid_response";
        public const string ERROR_SCORE_TOO_LOW = "score_too_low";
        public const string ERROR_SYSTEM_ERROR = "system_error";
        public const string ERROR_NOT_ENABLED = "not_enabled";
        public const string ERROR_VERIFICATION_SKIPPED = "verification_skipped";
    }
}