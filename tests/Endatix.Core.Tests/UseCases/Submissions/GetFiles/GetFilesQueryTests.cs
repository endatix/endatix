using Endatix.Core.UseCases.Submissions.GetFiles;
using FluentAssertions;
using Xunit;

namespace Endatix.Core.Tests.UseCases.Submissions.GetFiles;

public class GetFilesQueryTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 1;
        var submissionId = 2;
        var prefix = "prefix";

        // Act
        var query = new GetFilesQuery(formId, submissionId, prefix);

        // Assert
        query.FormId.Should().Be(formId);
        query.SubmissionId.Should().Be(submissionId);
        query.FileNamesPrefix.Should().Be(prefix);
    }

    [Fact]
    public void Constructor_AllowsNullPrefix()
    {
        // Arrange
        long formId = 1;
        long submissionId = 2;

        // Act
        var query = new GetFilesQuery(formId, submissionId, null);

        // Assert
        query.FileNamesPrefix.Should().BeNull();
    }
} 