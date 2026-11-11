namespace Endatix.Infrastructure.Utils;

public static class StringUtils
{
    private const char SNAKE_CASE_SEPARATOR = '_';
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
        var cursor = new KebabCursor(buffer);
        for (var i = 0; i < src.Length; i++)
        {
            cursor.Write(src[i]);
        }

        return cursor.Finish();
    }

    private ref struct KebabCursor(Span<char> buffer)
    {
        private readonly Span<char> _buffer = buffer;
        private int _pos;
        private bool _wasLower;

        public void Write(char c)
        {
            if (char.IsWhiteSpace(c) || c == '_' || c == '-')
            {
                WriteSeparator();
                _wasLower = false;
                return;
            }

            if (char.IsUpper(c))
            {
                WriteUpper(c);
                return;
            }

            _buffer[_pos++] = c;
            _wasLower = true;
        }

        public string Finish()
        {
            var pos = _pos;
            if (pos > 0 && _buffer[pos - 1] == '-')
            {
                pos--;
            }

            var start = pos > 0 && _buffer[0] == '-' ? 1 : 0;
            return new string(_buffer[start..pos]);
        }

        private void WriteUpper(char c)
        {
            // A previous lower-case character always leaves a non-separator in the buffer.
            if (_wasLower)
            {
                _buffer[_pos++] = '-';
            }

            _buffer[_pos++] = char.ToLowerInvariant(c);
            _wasLower = false;
        }

        private void WriteSeparator()
        {
            if (_pos > 0 && _buffer[_pos - 1] != '-')
            {
                _buffer[_pos++] = '-';
            }
        }
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
