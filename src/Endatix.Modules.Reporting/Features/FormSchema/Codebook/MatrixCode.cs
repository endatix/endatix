using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

internal readonly record struct MatrixCode(bool IsNumeric, string Value)
{
    internal static MatrixCode OfColumn(JsonElement column)
    {
        var value = column.GetProperty(SurveyJsPropertyNames.Value).GetString()!;
        return IsNumericColumn(column) ? Number(value) : Text(value);
    }

    internal static bool IsNumericColumn(JsonElement column) =>
        column.TryGetPropertyValue(FormSchemaCodebookPropertyNames.NUMERIC_CODE) is { ValueKind: JsonValueKind.True };

    internal static MatrixCode Text(string value) => new(false, value);

    // Signed integer coefficient and base-ten exponent, without expanding powers or rounding a significand.
    internal static MatrixCode Number(string token)
    {
        var e = token.AsSpan().IndexOfAny('e', 'E');
        var exponent = e < 0 ? BigInteger.Zero : BigInteger.Parse(token.AsSpan(e + 1), CultureInfo.InvariantCulture);
        var mantissa = e < 0 ? token : token[..e];
        var point = mantissa.IndexOf('.');
        var digits = mantissa.TrimStart('-').Replace(".", "").TrimStart('0');
        if (digits.Length == 0)
        {
            return new(true, "0");
        }

        var coefficient = digits.TrimEnd('0');
        exponent += digits.Length - coefficient.Length - (point < 0 ? 0 : mantissa.Length - point - 1);
        return new(true, string.Create(CultureInfo.InvariantCulture, $"{(mantissa[0] == '-' ? "-" : "")}{coefficient}e{exponent}"));
    }

    internal int? Ordinal(int count)
    {
        if (!decimal.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || n < 1 || n > count)
        {
            return null;
        }

        var index = (int)n;
        return this == Number(index.ToString(CultureInfo.InvariantCulture)) ? index : null;
    }
}
