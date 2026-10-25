using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Static class containing error codes and messages for submission access token errors.
/// </summary>
public static class SubmissionAccessTokenErrors
{
    public class ErrorCodes
    {
        public const string INVALID_PERMISSION = "invalid_permission";
        public const string INVALID_TOKEN = "invalid_access_token";
        public const string TOKEN_EXPIRED = "token_expired";
        /// <summary>A stamp that is not 1 to 64 lowercase hex characters.</summary>
        public const string INVALID_STAMP = "invalid_stamp";
    }

    public class Messages
    {
        public const string INVALID_TOKEN = "Invalid access token";
        public const string TOKEN_EXPIRED = "Token expired";
        /// <summary>Why a stamp was refused.</summary>
        public const string INVALID_STAMP = "A stamp is 1 to 64 lowercase hex characters";
    }

    public class ValidationErrors
    {
        public static readonly ValidationError InvalidToken = new ValidationError(
            "Token",
            Messages.INVALID_TOKEN,
            ErrorCodes.INVALID_TOKEN,
            ValidationSeverity.Error);

        public static readonly ValidationError TokenExpired = new ValidationError(
            "Token",
            Messages.TOKEN_EXPIRED,
            ErrorCodes.TOKEN_EXPIRED,
            ValidationSeverity.Error);

        /// <summary>A stamp a token cannot carry.</summary>
        public static readonly ValidationError InvalidStamp = new ValidationError(
            "Stamp",
            Messages.INVALID_STAMP,
            ErrorCodes.INVALID_STAMP,
            ValidationSeverity.Error);

        public static ValidationError InvalidPermission(string permission) => new ValidationError(
            "Permissions",
            $"Invalid permission: {permission}. Valid permissions are: view, edit, export",
            ErrorCodes.INVALID_PERMISSION,
            ValidationSeverity.Error);
    }
}
