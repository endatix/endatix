using System.Globalization;
using System.Text.RegularExpressions;

namespace Endatix.Core.Branding;

/// <summary>An sRGB colour, each channel in 0..1.</summary>
internal readonly record struct Rgb(double R, double G, double B);

/// <summary>OKLCH: lightness 0..1, chroma 0 or more, hue in degrees 0..360.</summary>
internal readonly record struct Oklch(double L, double C, double H);

/// <summary>
/// Colour maths for brand accents: CSS colour parsing, sRGB to and from OKLCH, and the WCAG 2.x contrast ratio.
/// A port of the Hub's <c>lib/branding/color.ts</c>; both must give the same answer for the same input.
/// </summary>
internal static partial class BrandColor
{
    private const string Number = @"[+-]?(?:\d+(?:\.\d*)?|\.\d+)";
    private const string Arg = "(" + Number + ")(%|deg)?";
    private const double OklchChromaPerPercent = 0.4 / 100;
    private const double GamutEpsilon = 1e-4;
    private const int GamutSearchSteps = 24;

    [GeneratedRegex(@"^#([0-9a-f]{3}|[0-9a-f]{6})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Hex();

    /// <summary>All commas: <c>rgb()</c> and <c>hsl()</c> only.</summary>
    [GeneratedRegex(@"^(rgb|hsl)a?\(\s*" + Arg + @"\s*,\s*" + Arg + @"\s*,\s*" + Arg + @"\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CommaArgs();

    /// <summary>All spaces: <c>rgb()</c>, <c>hsl()</c> and <c>oklch()</c>.</summary>
    [GeneratedRegex(@"^(rgb|hsl|oklch)a?\(\s*" + Arg + @"\s+" + Arg + @"\s+" + Arg + @"\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SpaceArgs();

    private readonly record struct ColorArg(double Value, string Unit);

    /// <summary>
    /// Reads <c>#rgb</c>, <c>#rrggbb</c>, <c>rgb()</c>, <c>hsl()</c> and <c>oklch()</c> (comma or space separated, not mixed;
    /// <c>oklch()</c> space only; no alpha), with the units CSS allows per argument. Null for anything else, named colours included.
    /// </summary>
    internal static Rgb? Parse(string input)
    {
        var value = input.Trim();
        var hex = Hex().Match(value);
        if (hex.Success)
        {
            return ParseHex(hex.Groups[1].Value);
        }

        var commas = CommaArgs().Match(value);
        var match = commas.Success ? commas : SpaceArgs().Match(value);
        return match.Success ? FromFunction(match, commas.Success) : null;
    }

    /// <summary><c>#rrggbb</c>, each channel rounded to the nearest byte.</summary>
    internal static string ToHex(Rgb color) =>
        $"#{Byte(color.R):x2}{Byte(color.G):x2}{Byte(color.B):x2}";

    /// <summary>The colour as <see cref="ToHex"/> writes it, read back: what a stylesheet holds.</summary>
    internal static Rgb AsWritten(Rgb color) => Parse(ToHex(color))!.Value;

    internal static Oklch ToOklch(Rgb color)
    {
        var r = ToLinear(color.R);
        var g = ToLinear(color.G);
        var b = ToLinear(color.B);
        var l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
        var m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
        var s = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));
        var lightness = (0.2104542553 * l) + (0.793617785 * m) - (0.0040720468 * s);
        var a = (1.9779984951 * l) - (2.428592205 * m) + (0.4505937099 * s);
        var bAxis = (0.0259040371 * l) + (0.7827717662 * m) - (0.808675766 * s);
        var chroma = Math.Sqrt((a * a) + (bAxis * bAxis));
        var hue = chroma < 1e-6 ? 0 : WrapHue(Math.Atan2(bAxis, a) * 180 / Math.PI);
        return new Oklch(lightness, chroma, hue);
    }

    /// <summary>
    /// OKLCH to sRGB. A colour outside the sRGB gamut keeps its lightness and hue and loses chroma until it fits.
    /// </summary>
    internal static Rgb FromOklch(Oklch color)
    {
        var lightness = Clamp01(color.L);
        var exact = OklchToRgbUnclamped(color with { L = lightness });
        if (InGamut(exact))
        {
            return ClampRgb(exact);
        }

        var chroma = MaxChromaInGamut(lightness, color.C, color.H);
        return ClampRgb(OklchToRgbUnclamped(new Oklch(lightness, chroma, color.H)));
    }

    /// <summary>Binary search for the largest chroma up to <paramref name="chroma"/> that stays inside sRGB.</summary>
    private static double MaxChromaInGamut(double lightness, double chroma, double hue)
    {
        var low = 0.0;
        var high = chroma;
        for (var i = 0; i < GamutSearchSteps; i++)
        {
            var mid = (low + high) / 2;
            if (InGamut(OklchToRgbUnclamped(new Oklch(lightness, mid, hue))))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    /// <summary>WCAG 2.x contrast ratio, 1..21.</summary>
    internal static double ContrastRatio(Rgb a, Rgb b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    internal static double WrapHue(double hue) => ((hue % 360) + 360) % 360;

    private static Rgb? FromFunction(Match match, bool commas)
    {
        var args = new[] { 2, 4, 6 }
            .Select(i => new ColorArg(
                double.Parse(match.Groups[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture),
                match.Groups[i + 1].Value.ToLowerInvariant()))
            .ToArray();
        if (args.Any(arg => !double.IsFinite(arg.Value)))
        {
            return null;
        }

        return match.Groups[1].Value.ToLowerInvariant() switch
        {
            "rgb" => RgbFromArgs(args, commas),
            "hsl" => HslFromArgs(args),
            _ => OklchFromArgs(args),
        };
    }

    private static Rgb? RgbFromArgs(ColorArg[] args, bool commas)
    {
        if (args.Any(arg => arg.Unit == "deg") || (commas && args.Any(arg => arg.Unit != args[0].Unit)))
        {
            return null;
        }

        var channels = args.Select(arg => Clamp01(arg.Unit == "%" ? arg.Value / 100 : arg.Value / 255)).ToArray();
        return new Rgb(channels[0], channels[1], channels[2]);
    }

    private static Rgb? HslFromArgs(ColorArg[] args)
    {
        var (h, s, l) = (args[0], args[1], args[2]);
        if (h.Unit == "%" || s.Unit == "deg" || l.Unit == "deg")
        {
            return null;
        }

        return HslToRgb(h.Value, s.Value / 100, l.Value / 100);
    }

    private static Rgb? OklchFromArgs(ColorArg[] args)
    {
        var (l, c, h) = (args[0], args[1], args[2]);
        if (l.Unit == "deg" || c.Unit == "deg" || h.Unit == "%")
        {
            return null;
        }

        return FromOklch(new Oklch(
            l.Unit == "%" ? l.Value / 100 : l.Value,
            // CSS clamps a negative chroma to 0 (neutral grey).
            Math.Max(0, c.Unit == "%" ? c.Value * OklchChromaPerPercent : c.Value),
            h.Value));
    }

    private static Rgb ParseHex(string digits)
    {
        var full = digits.Length == 3 ? string.Concat(digits.Select(d => $"{d}{d}")) : digits;
        double Channel(int i) => int.Parse(full.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return new Rgb(Channel(0), Channel(2), Channel(4));
    }

    private static Rgb HslToRgb(double hue, double saturation, double lightness)
    {
        var s = Clamp01(saturation);
        var l = Clamp01(lightness);
        var chroma = (1 - Math.Abs((2 * l) - 1)) * s;
        var h = WrapHue(hue) / 60;
        var x = chroma * (1 - Math.Abs((h % 2) - 1));
        var (r, g, b) = Math.Min(5, (int)Math.Floor(h)) switch
        {
            0 => (chroma, x, 0.0),
            1 => (x, chroma, 0.0),
            2 => (0.0, chroma, x),
            3 => (0.0, x, chroma),
            4 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };
        var m = l - (chroma / 2);
        return new Rgb(r + m, g + m, b + m);
    }

    private static Rgb OklchToRgbUnclamped(Oklch color)
    {
        var radians = color.H * Math.PI / 180;
        var a = color.C * Math.Cos(radians);
        var b = color.C * Math.Sin(radians);
        var l = Math.Pow(color.L + (0.3963377774 * a) + (0.2158037573 * b), 3);
        var m = Math.Pow(color.L - (0.1055613458 * a) - (0.0638541728 * b), 3);
        var s = Math.Pow(color.L - (0.0894841775 * a) - (1.291485548 * b), 3);
        return new Rgb(
            FromLinear((4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s)),
            FromLinear((-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s)),
            FromLinear((-0.0041960863 * l) - (0.7034186147 * m) + (1.707614701 * s)));
    }

    private static double RelativeLuminance(Rgb color) =>
        (0.2126 * ToLinear(color.R)) + (0.7152 * ToLinear(color.G)) + (0.0722 * ToLinear(color.B));

    private static bool InGamut(Rgb color) =>
        new[] { color.R, color.G, color.B }.All(v => v >= -GamutEpsilon && v <= 1 + GamutEpsilon);

    private static Rgb ClampRgb(Rgb color) => new(Clamp01(color.R), Clamp01(color.G), Clamp01(color.B));

    private static int Byte(double channel) => (int)Math.Round(Clamp01(channel) * 255, MidpointRounding.AwayFromZero);

    private static double Clamp01(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);

    private static double ToLinear(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static double FromLinear(double channel)
    {
        var abs = Math.Abs(channel);
        return abs <= 0.0031308
            ? channel * 12.92
            : Math.Sign(channel) * ((1.055 * Math.Pow(abs, 1 / 2.4)) - 0.055);
    }
}
