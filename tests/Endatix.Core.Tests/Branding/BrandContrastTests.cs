using Endatix.Core.Branding;
using FluentAssertions;

namespace Endatix.Core.Tests.Branding;

/// <summary>
/// The expected values come from the Hub's <c>lib/branding</c> (<c>derivePalette</c>, <c>checkBrandContrast</c>,
/// <c>parseCssColor</c>), run on the same inputs, so the API refuses exactly the accents the Hub would flag.
/// </summary>
public sealed class BrandContrastTests
{
    [Theory]
    [InlineData("#0052d1", "#0052d1")]
    [InlineData("#abc", "#aabbcc")]
    [InlineData("rgb(0 82 209)", "#0052d1")]
    [InlineData("rgb(10, 20, 30)", "#0a141e")]
    [InlineData("hsl(216 100% 41%)", "#0054d1")]
    [InlineData("oklch(0.5 0.2 260)", "#0559d2")]
    [InlineData("oklch(70% 40% 30deg)", "#f17260")]
    public void Parse_EachSyntaxTheHubReads_GivesTheHubsColour(string input, string expectedHex)
    {
        // Act
        var color = BrandColor.Parse(input);

        // Assert
        color.Should().NotBeNull();
        BrandColor.ToHex(color!.Value).Should().Be(expectedHex);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#1234")]
    [InlineData("rgb(1,2 3)")]
    [InlineData("hsl(1%,2,3)")]
    [InlineData("rgba(1,2,3,0.5)")]
    public void Parse_WhatTheHubRejects_IsNull(string input)
    {
        // Act
        var color = BrandColor.Parse(input);

        // Assert
        color.Should().BeNull();
    }

    [Theory]
    [InlineData("#0052d1")]
    [InlineData("#777777")]
    [InlineData("#949494")]
    [InlineData("#000000")]
    [InlineData("#e11d48")]
    [InlineData("#16a34a")]
    [InlineData("#8a2be2")]
    [InlineData("oklch(0.5 0.2 260)")]
    public void Check_AccentTheHubAccepts_HasNoFinding(string primary)
    {
        // Act
        var findings = BrandContrast.Check(BrandColor.Parse(primary)!.Value, null);

        // Assert
        findings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("#ffff00", 1.07)]
    [InlineData("#959595", 2.99)]
    [InlineData("#ffffff", 1)]
    [InlineData("#f59e0b", 2.14)]
    [InlineData("#abc", 1.96)]
    [InlineData("#00ffff", 1.25)]
    [InlineData("oklch(70% 40% 30deg)", 2.87)]
    public void Check_FaintAccent_IsFlaggedInLightModeWithTheHubsRatio(string primary, double ratio)
    {
        // Act
        var findings = BrandContrast.Check(BrandColor.Parse(primary)!.Value, null);

        // Assert
        findings.Should().ContainSingle().Which.Should().Be(new ContrastFinding("light", "primary", ratio, 3));
    }

    [Fact]
    public void Check_DarkAccentGiven_IsCheckedOnTheDarkPage()
    {
        // Act
        var findings = BrandContrast.Check(BrandColor.Parse("#0052d1")!.Value, BrandColor.Parse("#001a40")!.Value);

        // Assert
        findings.Should().ContainSingle().Which.Should().Be(new ContrastFinding("dark", "primary-on-dark", 1.12, 3));
    }

    [Fact]
    public void Check_ReadableDarkAccentGiven_HasNoFinding()
    {
        // Act
        var findings = BrandContrast.Check(BrandColor.Parse("#0052d1")!.Value, BrandColor.Parse("#7aa2ff")!.Value);

        // Assert
        findings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("#0052d1", "#b4c5ff")]
    [InlineData("#ffff00", "#bfd191")]
    [InlineData("#777777", "#c7c7c7")]
    [InlineData("#e11d48", "#fdb2a6")]
    [InlineData("#16a34a", "#a0d6b9")]
    [InlineData("#f59e0b", "#dec496")]
    [InlineData("#00ffff", "#97d4dc")]
    [InlineData("rgb(10, 20, 30)", "#c4c8ce")]
    public void DeriveDarkPrimary_NoDarkAccent_MatchesTheHubsDerivation(string primary, string expectedDark)
    {
        // Act
        var dark = BrandContrast.DeriveDarkPrimary(BrandColor.Parse(primary)!.Value);

        // Assert
        BrandColor.ToHex(dark).Should().Be(expectedDark);
    }
}
