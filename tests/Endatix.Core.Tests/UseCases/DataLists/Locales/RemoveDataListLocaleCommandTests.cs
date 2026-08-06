using Endatix.Core.UseCases.DataLists.Locales;

namespace Endatix.Core.Tests.UseCases.DataLists.Locales;

public class RemoveDataListLocaleCommandTests
{
    [Fact]
    public void Ctor_ValidArgs_AssignsProperties()
    {
        // Arrange & Act
        RemoveDataListLocaleCommand command = new(42, "es");

        // Assert
        command.DataListId.Should().Be(42);
        command.Locale.Should().Be("es");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_NonPositiveDataListId_Throws(long dataListId)
    {
        // Act
        Action act = () => _ = new RemoveDataListLocaleCommand(dataListId, "es");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_NullOrWhitespaceLocale_Throws(string? locale)
    {
        // Act
        Action act = () => _ = new RemoveDataListLocaleCommand(1, locale!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
