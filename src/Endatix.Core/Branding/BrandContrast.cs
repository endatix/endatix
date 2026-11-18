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

    /// <summary>Each colour mode and the page its primary sits on (<c>--background</c>).</summary>
    private static readonly ColorMode Light = new("light", BrandColor.Parse("#ffffff")!.Value);
    private static readonly ColorMode Dark = new("dark", BrandColor.Parse("#000f21")!.Value);

    private sealed record ColorMode(string Name, Rgb Background);

    internal static IReadOnlyList<ContrastFinding> Check(Rgb primary, Rgb? primaryOnDark)
    {
        var light = Finding(Light, PrimaryField, primary);
        var dark = primaryOnDark is { } onDark
            ? Finding(Dark, PrimaryOnDarkField, onDark)
            : Finding(Dark, PrimaryField, DeriveDarkPrimary(primary));
        return new[] { light, dark }.OfType<ContrastFinding>().ToList();
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

    private static ContrastFinding? Finding(ColorMode mode, string field, Rgb primary)
    {
        var ratio = BrandColor.ContrastRatio(BrandColor.AsWritten(primary), mode.Background);
        return ratio < UiContrast
            ? new ContrastFinding(mode.Name, field, Math.Floor(ratio * 100) / 100, UiContrast)
            : null;
    }
}
