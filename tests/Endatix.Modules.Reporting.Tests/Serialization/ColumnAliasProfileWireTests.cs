using Endatix.Modules.Reporting.Contracts.Export;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Serialization;

public sealed class ColumnAliasProfileWireTests
{
    [Theory]
    [InlineData(ColumnAliasProfile.Native, ColumnAliasProfileWire.Native)]
    [InlineData(ColumnAliasProfile.Crunch, ColumnAliasProfileWire.Crunch)]
    public void ToWireValue_ReturnsStableWireKeys(ColumnAliasProfile profile, string expectedWireValue)
    {
        ColumnAliasProfileWire.ToWireValue(profile).Should().Be(expectedWireValue);
    }

    [Theory]
    [InlineData(null, ColumnAliasProfile.Native)]
    [InlineData("", ColumnAliasProfile.Native)]
    [InlineData("   ", ColumnAliasProfile.Native)]
    [InlineData("native", ColumnAliasProfile.Native)]
    [InlineData("Native", ColumnAliasProfile.Native)]
    [InlineData("crunch", ColumnAliasProfile.Crunch)]
    [InlineData("CRUNCH", ColumnAliasProfile.Crunch)]
    public void TryParse_AcceptsKnownWireValues(string? wireValue, ColumnAliasProfile expected)
    {
        bool parsed = ColumnAliasProfileWire.TryParse(wireValue, out ColumnAliasProfile profile);

        parsed.Should().BeTrue();
        profile.Should().Be(expected);
    }

    [Theory]
    [InlineData("spss")]
    [InlineData("unknown")]
    public void TryParse_RejectsUnknownWireValues(string wireValue)
    {
        bool parsed = ColumnAliasProfileWire.TryParse(wireValue, out _);

        parsed.Should().BeFalse();
    }
}
