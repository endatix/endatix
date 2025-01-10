namespace Endatix.Core.Tests;

public enum ErrorType
{
    ZeroOrNegative,
    Negative,
    Null,
    Empty,
    SigningKeyEmpty,
    IssuerEmpty,
    AudienceEmpty,
    AccessTokenZeroOrNegative,
    RefreshTokenZeroOrNegative,
    InvalidFilterOperator,
    FilterNoField,
    FilterEmptyValue,
    PropertyNotExist,
}

public static class ErrorMessages
{
    private static readonly Dictionary<ErrorType, string> errorMessageTemplates = new Dictionary<ErrorType, string>
    {
        { ErrorType.ZeroOrNegative, "Required input {0} cannot be zero or negative. (Parameter '{0}')" },
        { ErrorType.Negative, "Required input {0} cannot be negative. (Parameter '{0}')" },
        { ErrorType.Null, "Value cannot be null. (Parameter '{0}')" },
        { ErrorType.Empty, "Required input {0} was empty. (Parameter '{0}')" },
        { ErrorType.SigningKeyEmpty, "Signing key cannot be empty. Please check your appSettings. (Parameter '{0}')" },
        { ErrorType.IssuerEmpty, "Issuer cannot be empty. Please check your appSettings (Parameter '{0}')" },
        { ErrorType.AudienceEmpty, "You need at least one audience in your appSettings. (Parameter '{0}')" },
        { ErrorType.AccessTokenZeroOrNegative, "Access Token expiration must be positive number representing minutes for access token lifetime (Parameter '{0}')" },
        { ErrorType.RefreshTokenZeroOrNegative, "Refresh Token expiration must be positive number representing days for refresh token lifetime (Parameter '{0}')" },
        { ErrorType.InvalidFilterOperator, "Invalid filter operator. Valid operators are: !:, >:, <:, :, >, < (Parameter '{0}')" },
        { ErrorType.FilterNoField, "Filter must have a field name (Parameter '{0}')" },
        { ErrorType.FilterEmptyValue, "Filter values cannot be empty (Parameter '{0}')" },
        { ErrorType.PropertyNotExist, "Property '{0}' does not exist on type 'TestEntity' (Parameter '{1}')" },
    };

    public static string GetErrorMessage(string fieldName, ErrorType errorType)
    {
        if (errorMessageTemplates.TryGetValue(errorType, out var template))
        {
            return string.Format(template, fieldName);
        }
        throw new ArgumentException("Invalid error type.", nameof(errorType));
    }

    public static string GetErrorMessage(ErrorType errorType, params string[] args)
    {
        if (errorMessageTemplates.TryGetValue(errorType, out var template))
        {
            return string.Format(template, args);
        }
        throw new ArgumentException("Invalid error type.", nameof(errorType));
    }
}
