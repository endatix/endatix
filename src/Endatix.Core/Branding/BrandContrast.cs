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
    internal const string PRIMARY_FIELD = "primary";
    internal const string PRIMARY_ON_DARK_FIELD = "primary-on-dark";

    /// <summary>WCAG 2.x 1.4.11: a control or focus ring against what is next to it.</summary>
    internal const double UI_CONTRAST = 3;

    private const double MAX_CHROMA_SCALE = 1.5;

    /// <summary>The built-in primaries the Hub derives from (<c>app/globals.css</c>).</summary>
    private static readonly Oklch _builtInLightPrimary = BrandColor.ToOklch(BrandColor.Parse("hsl(216 100% 41%)")!.Value);
    private static readonly Oklch _builtInDarkPrimary = BrandColor.ToOklch(BrandColor.Parse("#b4c5ff")!.Value);

    /// <summary>Each colour mode and the page its primary sits on (<c>--background</c>).</summary>
    private static readonly ColorMode _light = new("light", BrandColor.Parse("#ffffff")!.Value);
    private static readonly ColorMode _dark = new("dark", BrandColor.Parse("#000f21")!.Value);

    private sealed record ColorMode(string Name, Rgb Background);

    internal static IReadOnlyList<ContrastFinding> Check(Rgb primary, Rgb? primaryOnDark)
    {
        var light = Finding(_light, PRIMARY_FIELD, primary);
        var dark = primaryOnDark is { } onDark
            ? Finding(_dark, PRIMARY_ON_DARK_FIELD, onDark)
            : Finding(_dark, PRIMARY_FIELD, DeriveDarkPrimary(primary));
        return new[] { light, dark }.OfType<ContrastFinding>().ToList();
    }

    /// <summary>
    /// The dark primary the Hub derives when none is given: the built-in dark primary moved by the brand's hue shift,
    /// with its chroma scaled alike (capped).
    /// </summary>
    internal static Rgb DeriveDarkPrimary(Rgb primary)
    {
        var brand = BrandColor.ToOklch(primary);
        var shift = brand.H - _builtInLightPrimary.H;
        var scale = Math.Min(MAX_CHROMA_SCALE, brand.C / _builtInLightPrimary.C);
        return BrandColor.FromOklch(new Oklch(
            _builtInDarkPrimary.L,
            _builtInDarkPrimary.C * scale,
            BrandColor.WrapHue(_builtInDarkPrimary.H + shift)));
    }

    private static ContrastFinding? Finding(ColorMode mode, string field, Rgb primary)
    {
        var ratio = BrandColor.ContrastRatio(BrandColor.AsWritten(primary), mode.Background);
        return ratio < UI_CONTRAST
            ? new ContrastFinding(mode.Name, field, Math.Floor(ratio * 100) / 100, UI_CONTRAST)
            : null;
    }
}
