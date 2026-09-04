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
    [InlineData(SubmissionExportRow.SystemColumns.SubmitterId, "N/A", false)]
    [InlineData("question2", "42", false)]
    [InlineData("choiceId", "123456789012345678", true)]
    [InlineData("question1", "answer1", false)]
    public void ShouldWriteAsText_MatchesIdRules(string column, string value, bool expected)
    {
        Assert.Equal(expected, ExcelIdCell.ShouldWriteAsText(column, value));
    }
}
