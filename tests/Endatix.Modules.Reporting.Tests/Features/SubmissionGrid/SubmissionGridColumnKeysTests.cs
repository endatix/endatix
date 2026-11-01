using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Tests.Features.SubmissionGrid;

public sealed class SubmissionGridColumnKeysTests
{
    [Fact]
    public void Parse_TrimsDropsEmptyAndDuplicateKeys_KeepingOrder()
    {
        // Act
        var keys = SubmissionGridColumnKeys.Parse(" q2 ,q1,,q2, ");

        // Assert
        keys.Should().Equal("q2", "q1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Parse_Empty_ReturnsNoKeys(string? columns)
    {
        // Act
        var keys = SubmissionGridColumnKeys.Parse(columns);

        // Assert
        keys.Should().BeEmpty();
    }
}
