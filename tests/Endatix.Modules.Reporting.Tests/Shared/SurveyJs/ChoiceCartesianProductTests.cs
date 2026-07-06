using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Tests.Shared.SurveyJs;

public class ChoiceCartesianProductTests
{
    [Fact]
    public void EstimateCombinationCount_EmptyLevels_ReturnsOne()
    {
        // Act
        long count = ChoiceCartesianProduct.EstimateCombinationCount([]);

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public void EstimateCombinationCount_MultipliesLevelSizes()
    {
        // Act
        long count = ChoiceCartesianProduct.EstimateCombinationCount([2, 3, 4]);

        // Assert
        count.Should().Be(24);
    }

    [Fact]
    public void EstimateCombinationCount_EmptyLevel_ReturnsZero()
    {
        // Act
        long count = ChoiceCartesianProduct.EstimateCombinationCount([2, 0, 4]);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void EstimateCombinationCount_Overflow_ReturnsMaxValue()
    {
        long count = ChoiceCartesianProduct.EstimateCombinationCount(
            [int.MaxValue, int.MaxValue, int.MaxValue]);

        count.Should().Be(long.MaxValue);
    }

    [Fact]
    public void Enumerate_EmptyLevel_ReturnsNoCombinations()
    {
        IReadOnlyList<IReadOnlyList<string>> levels =
        [
            ["a", "b"],
            [],
        ];

        List<string[]> combinations = ChoiceCartesianProduct.Enumerate(levels).ToList();

        combinations.Should().BeEmpty();
    }

    [Fact]
    public void Enumerate_ProducesExpectedCombinations()
    {
        // Arrange
        IReadOnlyList<IReadOnlyList<string>> levels =
        [
            ["a", "b"],
            ["1", "2"],
        ];

        // Act
        List<string[]> combinations = ChoiceCartesianProduct.Enumerate(levels).ToList();

        // Assert
        combinations.Should().BeEquivalentTo(
            new List<string[]>
            {
                new[] { "a", "1" },
                new[] { "a", "2" },
                new[] { "b", "1" },
                new[] { "b", "2" },
            });
    }
}
