namespace Endatix.Infrastructure.Utils;

public static class StringUtils
{
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
}