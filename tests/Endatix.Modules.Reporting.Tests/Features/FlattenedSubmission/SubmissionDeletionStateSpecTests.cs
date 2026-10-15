using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

public class SubmissionDeletionStateSpecTests
{
    private const long SubmissionId = 500;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NegativeOrZeroTenantId_ThrowsArgumentException(long tenantId)
    {
        // Act
        var create = () => new SubmissionDeletionStateSpec(tenantId, SubmissionId);

        // Assert
        create.Should().Throw<ArgumentException>();
    }
}
