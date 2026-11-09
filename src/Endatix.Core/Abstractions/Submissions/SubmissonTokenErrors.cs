using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Static class containing error codes and messages for submission token errors.
/// </summary>
public static class SubmissonTokenErrors
{
    public static class ErrorCodes
    {
        public const string SUBMISSION_TOKEN_INVALID = "submission_token_invalid";
    }

    public static class Messages
    {
        public const string SUBMISSION_TOKEN_INVALID = "Invalid or expired token";
    }

    public static class ValidationErrors
    {
        public static readonly ValidationError SubmissionTokenInvalid = new ValidationError("Token", Messages.SUBMISSION_TOKEN_INVALID, ErrorCodes.SUBMISSION_TOKEN_INVALID, ValidationSeverity.Error);
    }
}
