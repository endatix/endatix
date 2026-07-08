namespace Endatix.Infrastructure.Utils;

public static class StringUtils
{
    private const char SNAKE_CASE_SEPARATOR = '_';
    private const char KEBAB_CASE_SEPARATOR = '-';
    private const char DOTTED_SEPARATOR = '.';
    /// <summary>
    /// Converts a string to kebab-case.
    /// </summary>
    /// <param name="input">The string to convert.</param>
    /// <returns>The kebab-cased string.</returns>
    public static string ToKebabCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> src = input;
        Span<char> buffer = src.Length <= 128 ? stackalloc char[src.Length * 2] : new char[src.Length * 2];
        var pos = 0;
        var wasLower = false;

        for (var i = 0; i < src.Length; i++)
        {
            var c = src[i];
            if (char.IsWhiteSpace(c) || c == '_' || c == '-')
            {
                if (pos > 0 && buffer[pos - 1] != '-')
                {
                    buffer[pos++] = '-';
                }
                wasLower = false;
            }
            else if (char.IsUpper(c))
            {
                if (wasLower && pos > 0 && buffer[pos - 1] != '-')
                {
                    buffer[pos++] = '-';
                }
                buffer[pos++] = char.ToLowerInvariant(c);
                wasLower = false;
            }
            else
            {
                buffer[pos++] = c;
                wasLower = true;
            }
        }
        if (pos > 0 && buffer[pos - 1] == '-')
        {
            pos--;
        }

        var start = 0;
        if (pos > 0 && buffer[0] == '-')
        {
            start = 1;
        }

        return new string(buffer[start..pos]);
    }

    /// <summary>
    /// Converts a snake_case string to PascalCase.
    /// </summary>
    /// <param name="input">The snake_case string to convert.</param>
    /// <returns>The PascalCase string.</returns>
    /// <example>
    /// "form_created" -> "FormCreated"
    /// "submission_completed" -> "SubmissionCompleted"
    /// </example>
    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var parts = input.Split('_');
        var result = string.Concat(parts.Select(part =>
            char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

        return result;
    }

    /// <summary>
    /// Converts a webhook snake_case event name to the dotted integration event type stored on the outbox.
    /// Only the first underscore is replaced; remaining segments keep snake_case (e.g. enabled_state_changed).
    /// </summary>
    /// <example>
    /// "form_created" -> "form.created"
    /// "form_enabled_state_changed" -> "form.enabled_state_changed"
    /// </example>
    public static string ToDottedEventType(string snakeCaseEventName)
    {
        if (string.IsNullOrEmpty(snakeCaseEventName))
        {
            return snakeCaseEventName;
        }

        var separatorIndex = snakeCaseEventName.IndexOf(SNAKE_CASE_SEPARATOR);
        return separatorIndex < 0
            ? snakeCaseEventName
            : string.Concat(
                snakeCaseEventName.AsSpan(0, separatorIndex),
                [DOTTED_SEPARATOR],
                snakeCaseEventName.AsSpan(separatorIndex + 1));
    }
}