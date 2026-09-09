using Endatix.Core.Common;

namespace Endatix.Core.Tests.Common;

public sealed class StringExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void NullIfWhiteSpace_Blank_ReturnsNull(string? value)
    {
        value.NullIfWhiteSpace().Should().BeNull();
    }

    [Fact]
    public void NullIfWhiteSpace_NonBlank_ReturnsSameInstance()
    {
        const string value = " keep ";

        value.NullIfWhiteSpace().Should().BeSameAs(value);
    }
}
