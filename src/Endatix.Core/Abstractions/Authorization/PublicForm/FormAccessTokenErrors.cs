using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Authorization.PublicForm;

/// <summary>
/// Errors for form access JWT issuance and validation.
/// </summary>
public static class FormAccessTokenErrors
{
    /// <summary>
    /// Error codes for form access JWT issuance and validation.
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>
        /// The form access token is invalid.
        /// </summary>
        public const string INVALID_TOKEN = "invalid_form_access_token";

        /// <summary>
        /// The form access token has expired.
        /// </summary>
        public const string TOKEN_EXPIRED = "form_access_token_expired";
    }

    /// <summary>
    /// Messages for form access JWT issuance and validation.
    /// </summary>
    public static class Messages
    {
        /// <summary>
        /// The form access token is invalid.
        /// </summary>
        public const string INVALID_TOKEN = "Invalid form access token";

        /// <summary>
        /// The form access token has expired.
        /// </summary>
        public const string TOKEN_EXPIRED = "Form access token expired";
    }

    /// <summary>
    /// Validation errors for form access JWT issuance and validation.
    /// </summary>
    public static class ValidationErrors
    {
        /// <summary>
        /// The form access token is invalid. Message and code are safe for API clients; do not attach raw parse or validation exceptions.
        /// </summary>
        public static readonly ValidationError InvalidToken = new(
            "Token",
            Messages.INVALID_TOKEN,
            ErrorCodes.INVALID_TOKEN,
            ValidationSeverity.Error);

        /// <summary>
        /// The form access token has expired.
        /// </summary>
        public static readonly ValidationError TokenExpired = new ValidationError(
            "Token",
            Messages.TOKEN_EXPIRED,
            ErrorCodes.TOKEN_EXPIRED,
            ValidationSeverity.Error);
    }
}
