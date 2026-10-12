using Endatix.Modules.Audience.Features.People;

namespace Endatix.Modules.Audience.Tests.Features.People;

public class ListPeopleHandlerTests
{
    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(10, 5, 6)]
    [InlineData(5, 0, 7)]
    public void TotalCoveringPage_PageHoldsRowsTheCountMissed_CoversEveryRowShown(int counted, int offset, int shown)
    {
        // Arrange
        // Inputs come from InlineData.

        // Act
        int total = ListPeopleHandler.TotalCoveringPage(counted, offset, shown);

        // Assert
        total.Should().Be(offset + shown);
    }

    [Theory]
    [InlineData(12, 5, 5)]
    [InlineData(0, 0, 0)]
    [InlineData(3, 0, 2)]
    public void TotalCoveringPage_CountCoversPage_KeepsTheCount(int counted, int offset, int shown)
    {
        // Arrange
        // Inputs come from InlineData.

        // Act
        int total = ListPeopleHandler.TotalCoveringPage(counted, offset, shown);

        // Assert
        total.Should().Be(counted);
    }
}
