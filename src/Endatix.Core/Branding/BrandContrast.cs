namespace Endatix.Core.Branding;

/// <summary>A brand accent that is too faint against the page it sits on in one colour mode.</summary>
/// <param name="Mode"><c>light</c> or <c>dark</c>.</param>
/// <param name="Field">The override field to change: <c>primary</c>, or <c>primary-on-dark</c> when it was given for dark mode.</param>
/// <param name="Ratio">The WCAG 2.x contrast ratio, rounded down to two decimals.</param>
/// <param name="Minimum">The ratio the accent must reach.</param>
internal sealed record ContrastFinding(string Mode, string Field, double Ratio, double Minimum);

/// <summary>
/// The Hub's accent check (<c>checkBrandContrast</c> in <c>lib/branding/palette.ts</c>): the primary the Hub derives for each
/// mode against that mode's page background, at the WCAG 2.x 1.4.11 minimum for controls and focus rings.
/// </summary>
internal static class BrandContrast
{
    internal const string PrimaryField = "primary";
    internal const string PrimaryOnDarkField = "primary-on-dark";

    /// <summary>WCAG 2.x 1.4.11: a control or focus ring against what is next to it.</summary>
    internal const double UiContrast = 3;

    private const double MaxChromaScale = 1.5;

    /// <summary>The built-in primaries the Hub derives from (<c>app/globals.css</c>).</summary>
    private static readonly Oklch BuiltInLightPrimary = BrandColor.ToOklch(BrandColor.Parse("hsl(216 100% 41%)")!.Value);
    private static readonly Oklch BuiltInDarkPrimary = BrandColor.ToOklch(BrandColor.Parse("#b4c5ff")!.Value);

    /// <summary>The page each mode's primary sits on (<c>--background</c>).</summary>
    private static readonly Rgb LightBackground = BrandColor.Parse("#ffffff")!.Value;
    private static readonly Rgb DarkBackground = BrandColor.Parse("#000f21")!.Value;

    internal static IReadOnlyList<ContrastFinding> Check(Rgb primary, Rgb? primaryOnDark)
    {
        var darkPrimary = primaryOnDark ?? DeriveDarkPrimary(primary);
        var findings = new List<ContrastFinding>();
        AddIfLow(findings, "light", PrimaryField, primary, LightBackground);
        AddIfLow(findings, "dark", primaryOnDark is null ? PrimaryField : PrimaryOnDarkField, darkPrimary, DarkBackground);
        return findings;
    }

    /// <summary>
    /// The dark primary the Hub derives when none is given: the built-in dark primary moved by the brand's hue shift,
    /// with its chroma scaled alike (capped).
    /// </summary>
    internal static Rgb DeriveDarkPrimary(Rgb primary)
    {
        var brand = BrandColor.ToOklch(primary);
        var shift = brand.H - BuiltInLightPrimary.H;
        var scale = Math.Min(MaxChromaScale, brand.C / BuiltInLightPrimary.C);
        return BrandColor.FromOklch(new Oklch(
            BuiltInDarkPrimary.L,
            BuiltInDarkPrimary.C * scale,
            BrandColor.WrapHue(BuiltInDarkPrimary.H + shift)));
    }

    private static void AddIfLow(List<ContrastFinding> findings, string mode, string field, Rgb primary, Rgb background)
    {
        var ratio = BrandColor.ContrastRatio(BrandColor.AsWritten(primary), background);
        if (ratio < UiContrast)
        {
            findings.Add(new ContrastFinding(mode, field, Math.Floor(ratio * 100) / 100, UiContrast));
        }
    }
}
