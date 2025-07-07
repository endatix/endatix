using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Features.ReCaptcha;

public static class ReCaptchaErrors
{
    public class ErrorCodes
    {
        public const string RECAPTCHA_VERIFICATION_FAILED = "recaptcha_verification_failed";
    }

    public class Messages
    {
        public const string RECAPTCHA_VERIFICATION_FAILED = "reCAPTCHA verification failed";
    }

    public class ValidationErrors
    {
        public static readonly ValidationError ReCaptchaVerificationFailed = new ValidationError(
            "reCAPTCHA",
            Messages.RECAPTCHA_VERIFICATION_FAILED,
            ErrorCodes.RECAPTCHA_VERIFICATION_FAILED,
            ValidationSeverity.Error
        );
    }
}