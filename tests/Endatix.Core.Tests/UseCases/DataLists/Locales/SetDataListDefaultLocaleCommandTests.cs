using Endatix.Core.UseCases.DataLists.Locales;

namespace Endatix.Core.Tests.UseCases.DataLists.Locales;

public class SetDataListDefaultLocaleCommandTests
{
    [Fact]
    public void Ctor_ValidArgs_AssignsProperties()
    {
        // Arrange & Act
        SetDataListDefaultLocaleCommand command = new(42, "en");

        // Assert
        command.DataListId.Should().Be(42);
        command.DefaultLocale.Should().Be("en");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_NonPositiveDataListId_Throws(long dataListId)
    {
        // Act
        Action act = () => _ = new SetDataListDefaultLocaleCommand(dataListId, "en");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_NullOrWhitespaceDefaultLocale_Throws(string? defaultLocale)
    {
        // Act
        Action act = () => _ = new SetDataListDefaultLocaleCommand(1, defaultLocale!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
