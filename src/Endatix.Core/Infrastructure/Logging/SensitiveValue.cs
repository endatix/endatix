namespace Endatix.Core.Infrastructure.Logging;

/// <summary>
/// A value that is sensitive and should be masked when logging.
/// </summary>
/// <param name="Value">The value to redact.</param>
/// <param name="Type">The type of sensitivity for the value.</param>
/// <returns>The redacted string.</returns>
public record SensitiveValue(object? Value, SensitivityType Type = SensitivityType.Generic)
{
    public override string ToString()
    {
        return PiiRedactor.Redact(Value, Type);
    }

    /// <summary>
    /// Creates a new SensitiveValue for an email.
    /// </summary>
    /// <param name="email">The email to redact.</param>
    /// <returns>The redacted email.</returns>
    public static SensitiveValue Email(string email)
    {
        return new SensitiveValue(email, SensitivityType.Email);
    }

    /// <summary>
    /// Creates a new SensitiveValue for a secret.
    /// </summary>
    /// <param name="secret">The secret to redact.</param>
    /// <returns>The redacted secret.</returns>
    public static SensitiveValue Secret(string secret)
    {
        return new SensitiveValue(secret, SensitivityType.Secret);
    }

    /// <summary>
    /// Creates a new SensitiveValue for a phone number.
    /// </summary>
    /// <param name="phoneNumber">The phone number to redact.</param>
    /// <returns>The redacted phone number.</returns>
    public static SensitiveValue PhoneNumber(string phoneNumber)
    {
        return new SensitiveValue(phoneNumber, SensitivityType.PhoneNumber);
    }

    /// <summary>
    /// Creates a new SensitiveValue for a name.
    /// </summary>
    /// <param name="name">The name to redact.</param>
    /// <returns>The redacted name.</returns>
    public static SensitiveValue Name(string name)
    {
        return new SensitiveValue(name, SensitivityType.Name);
    }
}