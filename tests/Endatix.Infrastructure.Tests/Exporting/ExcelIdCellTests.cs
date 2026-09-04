using Endatix.Core.Entities;
using Endatix.Infrastructure.Exporting;

namespace Endatix.Infrastructure.Tests.Exporting;

public sealed class ExcelIdCellTests
{
    [Theory]
    [InlineData(SubmissionExportRow.SystemColumns.Id, "1", true)]
    [InlineData(SubmissionExportRow.SystemColumns.FormId, "100", true)]
    [InlineData(SubmissionExportRow.SystemColumns.SubmitterId, "9", true)]
    [InlineData(SubmissionExportRow.SystemColumns.SubmitterDisplayId, "ext", true)]
    [InlineData("choiceId", "123456789012345678", true)]
    [InlineData("question2", "42", false)]
    [InlineData("question1", "answer1", false)]
    [InlineData("mixed", "1234567890123456x", false)]
    public void ShouldWriteAsText_ForColumnAndValue_MatchesIdRules(string column, string value, bool expected)
    {
        // Arrange
        // Act
        var writeAsText = ExcelIdCell.ShouldWriteAsText(column, value);

        // Assert
        Assert.Equal(expected, writeAsText);
    }
}
