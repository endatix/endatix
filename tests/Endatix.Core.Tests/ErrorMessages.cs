namespace Endatix.Core.Tests;

public enum ErrorType
{
    ZeroOrNegative,
    Null,
    Empty,
}

public static class ErrorMessages
{
    private static readonly Dictionary<ErrorType, string> errorMessageTemplates = new Dictionary<ErrorType, string>
    {
        { ErrorType.ZeroOrNegative, "Required input {0} cannot be zero or negative. (Parameter '{0}')" },
        { ErrorType.Null, "Value cannot be null. (Parameter '{0}')" },
        { ErrorType.Empty, "Required input {0} was empty. (Parameter '{0}')" }
    };

    public static string GetErrorMessage(string fieldName, ErrorType errorType)
    {
        if (errorMessageTemplates.TryGetValue(errorType, out var template))
        {
            return string.Format(template, fieldName);
        }
        throw new ArgumentException("Invalid error type.", nameof(errorType));
    }
}
