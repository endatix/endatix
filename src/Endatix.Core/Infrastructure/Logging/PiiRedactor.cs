namespace Endatix.Core.Infrastructure.Logging;

/// <summary>
/// Provides methods to redact sensitive data when logging.
/// <example>
/// <code>
/// var redactedEmail = PiiRedactor.Redact("john.doe@example.com", SensitivityType.Email);
/// Console.WriteLine(redactedEmail); // Output: j***@example.com
/// </code>
/// </example>
/// </summary>
public static class PiiRedactor
{
    internal const int SECRET_LENGTH_MIN_LENGTH = 5;
    internal const int SECRET_LENGTH_MAX_LENGTH = 16;
    internal const char PHONE_NUMBER_COUNTRY_CODE_PREFIX = '+';
    internal const byte PHONE_NUMBER_VISIBLE_DIGITS = 4;
    internal const byte PHONE_NUMBER_COUNTRY_CODE_MAX_LENGTH = 3;

    private static readonly int _phoneNumberWithCountryCodeMinCharsCount = PHONE_NUMBER_COUNTRY_CODE_MAX_LENGTH + 1 + PHONE_NUMBER_VISIBLE_DIGITS;

    private static readonly Random _randomGenerator = new();

    /// <summary>
    /// Redacts the sensitive data in the input string based on the sensitivity type.
    /// </summary>
    /// <param name="value">The input string to redact.</param>
    /// <param name="sensitivityType">The sensitivity type of the input string.</param>
    /// <returns>The redacted string.</returns>
    public static string Redact(object? value, SensitivityType sensitivityType)
    {
        if (value == null)
        {
            return string.Empty;
        }

        var input = value.ToString() ?? string.Empty;

        switch (sensitivityType)
        {
            case SensitivityType.Email:
                return RedactEmail(input);
            case SensitivityType.Secret:
                return RedactSecret();
            case SensitivityType.PhoneNumber:
                return RedactPhoneNumber(input);
            case SensitivityType.Name:
                return RedactName(input);
            default:
                return new string('*', input.Length);
        }
    }

    /// <summary>
    /// Redacts an email by replacing the username with asterisks and keeping the domain.
    /// </summary>
    /// <param name="email">The email to redact.</param>
    /// <returns>The redacted email.</returns>
    internal static string RedactEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2)
        {
            return "*****@*****";
        }

        var username = parts[0];
        var domain = parts[1];

        if (username.Length <= 2)
        {
            return $"*@{domain}";
        }

        return $"{username[0]}*****{username[^1]}@{domain}";
    }

    /// <summary>
    /// Redacts a secret by replacing it with a random string of asterisks to avoid hinting the actual length of the secret.
    /// </summary>
    /// <returns>A random string of asterisks.</returns>
    internal static string RedactSecret()
    {
        var maskedSecretLength = _randomGenerator.Next(SECRET_LENGTH_MIN_LENGTH, SECRET_LENGTH_MAX_LENGTH);
        return new string('*', maskedSecretLength);
    }

    /// <summary>
    /// Redacts a phone number by replacing the visible digits with asterisks.
    /// </summary>
    /// <param name="value">The phone number to redact.</param>
    /// <returns>The redacted phone number.</returns>
    internal static string RedactPhoneNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var chars = value.ToCharArray();

        var digitIndexes = chars
            .Select((character, index) => new { character, index })
            .Where(x => char.IsDigit(x.character))
            .Select(x => x.index)
            .ToList();

        if (digitIndexes.Count < PHONE_NUMBER_VISIBLE_DIGITS)
        {
            return new string('*', value.Length);
        }

        var countryCodeDigitsCount = DetectCountryCodeDigitsCount(chars);
        var canExposeCountryCode =
         countryCodeDigitsCount > 0 &&
         digitIndexes.Count > PHONE_NUMBER_VISIBLE_DIGITS + PHONE_NUMBER_COUNTRY_CODE_MAX_LENGTH;

        var digitsToKeepIndexes = new HashSet<int>();

        foreach (var index in digitIndexes.TakeLast(PHONE_NUMBER_VISIBLE_DIGITS))
        {
            digitsToKeepIndexes.Add(index);
        }

        if (canExposeCountryCode)
        {
            foreach (var index in digitIndexes.Take(countryCodeDigitsCount))
            {
                digitsToKeepIndexes.Add(index);
            }
        }

        foreach (var index in digitIndexes)
        {
            if (!digitsToKeepIndexes.Contains(index))
            {
                chars[index] = '*';
            }
        }

        return new string(chars);
    }

    private static int DetectCountryCodeDigitsCount(char[] chars)
    {
        if (chars.Length == 0 || chars[0] != PHONE_NUMBER_COUNTRY_CODE_PREFIX)
        {
            return 0;
        }

        var count = 0;
        for (var i = 1; i < chars.Length && count < PHONE_NUMBER_COUNTRY_CODE_MAX_LENGTH; i++)
        {
            if (!char.IsDigit(chars[i]))
            {
                break;
            }
            count++;
        }

        return count;
    }

    /// <summary>
    /// Redacts a name by replacing the first letter of each part with an asterisk.
    /// </summary>
    /// <param name="value">The name to redact.</param>
    /// <returns>The redacted name.</returns>
    internal static string RedactName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var nameParts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(" ",
            nameParts.Select(RedactNamePart));
    }

    private static string RedactNamePart(string part)
    {
        if (part.Length == 1)
        {
            return "*";
        }

        return part[0] + new string('*', part.Length - 1);
    }
}